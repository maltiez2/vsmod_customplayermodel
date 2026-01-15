using PlayerModelLib;
using ProtoBuf;
using System.Diagnostics;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.Client;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace CustomPlayerModel;

public sealed class CustomPlayerModelClientSystem : ModSystem
{
    public override double ExecuteOrder() => 0.22;
    
    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        _clientChannel = _clientApi.Network.RegisterChannel("CustomPlayerModel:model")
            .RegisterMessageType<CustomModelPacket>()
            .RegisterMessageType<CustomModelRequestPacket>()
            .SetMessageHandler<CustomModelPacket>(HandlePacketClient);

        string rootDirectory = Path.Combine(GamePaths.ModConfig, _customModelsFolder);

        if (!Directory.Exists(rootDirectory))
        {
            try
            {
                Directory.CreateDirectory(rootDirectory);
            }
            catch (UnauthorizedAccessException exception)
            {
                LoggerUtil.Error(api, this, $"Unable to create directory for custom models: Insufficient permissions\n{exception}");
                return;
            }
            catch (IOException exception)
            {
                LoggerUtil.Error(api, this, $"Unable to create directory for custom models:\n{exception}");
                return;
            }
        }

        string playerUid = api.Settings.String["playeruid"].Select(character => ((ushort)character).ToString()).Aggregate((f, s) => $"{f}{s}");

        foreach (string folderPath in Directory.GetDirectories(rootDirectory))
        {
            CustomModelPacket modelData = new()
            {
                Id = Path.GetFileName(folderPath.Replace('-', '_').ToLower()) + "-" + playerUid,
                ModelName = Path.GetFileName(folderPath.Replace('-', '_')),
                Config = ReadFileIfExists(Path.Combine(folderPath, "config.json")),
                Shape = ReadFileIfExists(Path.Combine(folderPath, "custom-shape.json")),
                Texture = ReadFileIfExists(Path.Combine(folderPath, "custom-texture.png")),
                Sound = ReadFileIfExists(Path.Combine(folderPath, "custom-voice.ogg"))
            };
            _modelsData[modelData.Id] = modelData;

            modelData.Shape = Encoding.UTF8.GetBytes(Asset.BytesToString(modelData.Shape).Replace("\"custom-texture\"", $"\"customplayermodel:{modelData.Id}\""));
        }

        foreach ((_, CustomModelPacket data) in _modelsData)
        {
            _assetsManager.Add(data, api);
        }
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _serverApi = api;
        _serverChannel = _serverApi.Network.RegisterChannel("CustomPlayerModel:model")
            .RegisterMessageType<CustomModelPacket>()
            .RegisterMessageType<CustomModelRequestPacket>()
            .SetMessageHandler<CustomModelPacket>(HandlePacketServer)
            .SetMessageHandler<CustomModelRequestPacket>(HandlePacketServer);
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        foreach ((string id, CustomModelPacket data) in _modelsData)
        {
            RegisterModel(api, id, data, enabled: true);
            _clientChannel?.SendPacket(data);
        }

        _clientChannel?.SendPacket(new CustomModelRequestPacket());
    }

    private const string _customModelsFolder = "custom-player-models";
    private readonly Dictionary<string, CustomModelPacket> _modelsData = [];
    private readonly AssetsManager _assetsManager = new();
    private const string _domain = "customplayermodel";
    private ICoreClientAPI? _clientApi;
    private IClientNetworkChannel? _clientChannel;
    private ICoreServerAPI? _serverApi;
    private IServerNetworkChannel? _serverChannel;

    private static byte[] ReadFileIfExists(string path)
    {
        return File.Exists(path) ? File.ReadAllBytes(path) : [];
    }

    private void HandlePacketClient(CustomModelPacket packet)
    {
        Debug.WriteLine($"\nHandlePacketClient: {packet.Id}\n");
        if (_modelsData.ContainsKey(packet.Id) || _clientApi == null)
        {
            return;
        }

        _modelsData.Add(packet.Id, packet);
        _assetsManager.Add(packet, _clientApi);
        RegisterModel(_clientApi, packet.Id, packet, enabled: false);
    }

    private void HandlePacketServer(IServerPlayer player, CustomModelPacket packet)
    {
        Debug.WriteLine($"\nHandlePacketServer: {packet.Id}\n");
        if (_modelsData.ContainsKey(packet.Id) || _serverApi == null)
        {
            return;
        }

        _modelsData.Add(packet.Id, packet);
        _assetsManager.Add(packet, _serverApi);
        RegisterModel(_serverApi, packet.Id, packet, enabled: false);

        _serverChannel?.BroadcastPacket(packet, player);
    }

    private void HandlePacketServer(IServerPlayer player, CustomModelRequestPacket packet)
    {
        Debug.WriteLine($"\nHandlePacketServer: CustomModelRequestPacket\n");
        foreach ((_, CustomModelPacket? data) in _modelsData)
        {
            _serverChannel?.SendPacket(data, player);
        }
    }

    private void RegisterModel(ICoreAPI api, string id, CustomModelPacket data, bool enabled)
    {
        Debug.WriteLine($"\nRegisterModel: {id}\n");
        CustomModelsSystem system = api.ModLoader.GetModSystem<CustomModelsSystem>();

        string configText = Asset.BytesToString(data.Config);
        CustomPlayerModelConfig customConfig = JsonObject.FromJson(configText).AsObject<CustomPlayerModelConfig>();
        CustomModelConfig config = GetConfig(id, customConfig);
        config.Enabled = enabled;
        config.Name = data.ModelName;

        system.HotLoadCustomModel(id, config);
    }

    private static CustomModelConfig GetConfig(string id, CustomPlayerModelConfig config)
    {
        SkinnablePartExtended[] skinnableParts = [
            new SkinnablePartExtended()
            {
                Code = "voicetype",
                Type = EnumSkinnableType.Voice,
                UseDropDown = true,
                Variants = [
                    new()
                    {
                        Code = "default",
                        Sound = $"{_domain}:sounds/{id}",
                    }
                ]
            },
            new SkinnablePartExtended()
            {
                Code = "voicepitch",
                Type = EnumSkinnableType.Voice,
                UseDropDown = true,
                Colbreak = true,
                Variants = [
                    new()
                    {
                        Code = "verylow"
                    },
                    new()
                    {
                        Code = "low"
                    },
                    new()
                    {
                        Code = "medium"
                    },
                    new()
                    {
                        Code = "high"
                    }
                ]
            }
        ];

        return new()
        {
            Domain = _domain,
            Group = _domain,
            Icon = "",
            GroupIcon = "",
            ShapePath = $"{_domain}:shapes/{id}.json",
            BaseShapeCode = config.BaseShapeCode,
            SkinnableParts = skinnableParts,
            WearableModelReplacers = config.WearableModelReplacers,
            WearableCompositeModelReplacers = config.WearableCompositeModelReplacers,
            WearableModelReplacersByShape = config.WearableModelReplacersByShape,
            CollisionBox = config.CollisionBox,
            EyeHeight = config.EyeHeight,
            SizeRange = config.SizeRange,
            ScaleColliderWithSizeHorizontally = config.ScaleColliderWithSizeHorizontally,
            ScaleColliderWithSizeVertically = config.ScaleColliderWithSizeVertically,
            MaxCollisionBox = config.MaxCollisionBox,
            MinCollisionBox = config.MinCollisionBox,
            MaxEyeHeight = config.MaxEyeHeight,
            MinEyeHeight = config.MinEyeHeight,
            AddTags = config.AddTags,
            RemoveTags = config.RemoveTags,
            ModelSizeFactor = config.ModelSizeFactor,
            HeadBobbingScale = config.HeadBobbingScale,
            GuiModelScale = config.GuiModelScale,
            WalkEyeHeightMultiplier = config.WalkEyeHeightMultiplier,
            SprintEyeHeightMultiplier = config.SprintEyeHeightMultiplier,
            SneakEyeHeightMultiplier = config.SneakEyeHeightMultiplier,
            StepHeight = config.StepHeight
        };
    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class CustomModelPacket
{
    public string ModelName { get; set; } = "";
    public string Id { get; set; } = "";
    public byte[] Config { get; set; } = [];
    public byte[] Shape { get; set; } = [];
    public byte[] Texture { get; set; } = [];
    public byte[] Sound { get; set; } = [];
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class CustomModelRequestPacket
{
    public bool Data { get; set; } = false;
}

public sealed class CustomPlayerModelConfig
{
    public int Version { get; set; } = 0;
    public string BaseShapeCode { get; set; } = "";
    public Dictionary<string, string> WearableModelReplacers { get; set; } = [];
    public Dictionary<string, CompositeShape> WearableCompositeModelReplacers { get; set; } = [];
    public Dictionary<string, string> WearableModelReplacersByShape { get; set; } = [];
    public float[] CollisionBox { get; set; } = [];
    public float EyeHeight { get; set; } = 1.7f;
    public float[] SizeRange { get; set; } = [0.8f, 1.2f];
    public bool ScaleColliderWithSizeHorizontally { get; set; } = true;
    public bool ScaleColliderWithSizeVertically { get; set; } = true;
    public float[] MaxCollisionBox { get; set; } = [float.MaxValue, float.MaxValue];
    public float[] MinCollisionBox { get; set; } = [0, 0];
    public float MaxEyeHeight { get; set; } = float.MaxValue;
    public float MinEyeHeight { get; set; } = 0;
    public string[] AddTags { get; set; } = [];
    public string[] RemoveTags { get; set; } = [];
    public float ModelSizeFactor { get; set; } = 1;
    public float HeadBobbingScale { get; set; } = 1;
    public float GuiModelScale { get; set; } = 1;
    public float WalkEyeHeightMultiplier { get; set; } = 1;
    public float SprintEyeHeightMultiplier { get; set; } = 1;
    public float SneakEyeHeightMultiplier { get; set; } = 0.8f;
    public float StepHeight { get; set; } = 0.6f;
}

public sealed class AssetsManager
{
    public void Add(CustomModelPacket packet, ICoreAPI api)
    {
        string id = packet.Id;

        Shapes[id] = CreateAsset($"customplayermodel:shapes/{id}.json", packet.Shape, Origin, api);
        Textures[id] = CreateAsset($"customplayermodel:textures/{id}.png", packet.Texture, Origin, api);
        Sounds[id] = CreateAsset($"customplayermodel:sounds/{id}.ogg", packet.Sound, Origin, api);

        ScreenManager.LoadSound(Sounds[id]);
    }

    private Dictionary<string, IAsset> Shapes = [];
    private Dictionary<string, IAsset> Textures = [];
    private Dictionary<string, IAsset> Sounds = [];
    private SynchronizedDataOrigin Origin = new();

    private static IAsset CreateAsset(string path, byte[] data, SynchronizedDataOrigin origin, ICoreAPI api)
    {
        Asset asset = new(data, path, origin);

        origin.Assets[path] = asset;
        origin.Data[path] = data;

        api.Assets.Add(path, asset);

        return asset;
    }
}

public sealed class SynchronizedDataOrigin : IAssetOrigin
{
    public Dictionary<string, IAsset> Assets { get; set; } = [];
    public Dictionary<string, byte[]> Data { get; set; } = [];

    string IAssetOrigin.OriginPath => "";
    List<IAsset> IAssetOrigin.GetAssets(AssetCategory category, bool shouldLoad) => Assets.Values.ToList();
    List<IAsset> IAssetOrigin.GetAssets(AssetLocation baseLocation, bool shouldLoad) => Assets.Values.ToList();
    bool IAssetOrigin.IsAllowedToAffectGameplay() => true;
    void IAssetOrigin.LoadAsset(IAsset asset) => asset.Data = Data[asset.Location];
    bool IAssetOrigin.TryLoadAsset(IAsset asset) => (asset.Data = Data[asset.Location]) != null;
}