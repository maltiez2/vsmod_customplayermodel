using PlayerModelLib;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.Common;

namespace CustomPlayerModel;

public sealed class CustomPlayerModelClientSystem : ModSystem
{
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

        string playerName = api.World.Player.PlayerName.Replace('@', '_');

        foreach (string folderPath in Directory.GetDirectories(rootDirectory))
        {
            CustomModelPacket modelData = new()
            {
                Id = Path.GetFileName(folderPath.Replace('@', '_')) + "@" + playerName,
                ModelName = Path.GetFileName(folderPath.Replace('@', '_')),
                Config = ReadFileIfExists(Path.Combine(folderPath, "config.json")),
                Shape = ReadFileIfExists(Path.Combine(folderPath, "shape.json")),
                Texture = ReadFileIfExists(Path.Combine(folderPath, "texture.png")),
                Sounds = ReadFileIfExists(Path.Combine(folderPath, "voice.ogg"))
            };
            _modelsData[modelData.Id] = modelData;
        }

        foreach ((_, CustomModelPacket data) in _modelsData)
        {
            _assetsManager.Add(data);
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
            RegisterModel(api, id, data);
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
        if (_modelsData.ContainsKey(packet.Id) || _clientApi == null)
        {
            return;
        }

        _assetsManager.Add(packet);
        RegisterModel(_clientApi, packet.Id, packet);
    }

    private void HandlePacketServer(IServerPlayer player, CustomModelPacket packet)
    {
        if (_modelsData.ContainsKey(packet.Id) || _serverApi == null)
        {
            return;
        }

        _assetsManager.Add(packet);
        RegisterModel(_serverApi, packet.Id, packet);

        _serverChannel?.BroadcastPacket(packet, player);
    }

    private void HandlePacketServer(IServerPlayer player, CustomModelRequestPacket packet)
    {
        foreach ((_, CustomModelPacket? data) in _modelsData)
        {
            _serverChannel?.SendPacket(packet, player);
        }
    }

    private void RegisterModel(ICoreAPI api, string id, CustomModelPacket data)
    {
        CustomModelsSystem system = api.ModLoader.GetModSystem<CustomModelsSystem>();

        string configText = Asset.BytesToString(data.Config);
        CustomPlayerModelConfig customConfig = JsonObject.FromJson(configText).AsObject<CustomPlayerModelConfig>();
        CustomModelConfig config = GetConfig(id, customConfig);

        system.HotLoadCustomModel(id, config);
    }

    private static CustomModelConfig GetConfig(string id, CustomPlayerModelConfig config)
    {
        return new()
        {
            Domain = _domain,
            Group = _domain,
            Icon = "",
            GroupIcon = "",
            ShapePath = $"{_domain}:shapes/{id}.json",
            BaseShapeCode = config.BaseShapeCode,
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

public sealed class CustomPlayerModelServerSystem : ModSystem
{
    public override void StartServerSide(ICoreServerAPI api)
    {
        _api = api;
        _channel = _api.Network.RegisterChannel("CustomPlayerModel:model")
            .RegisterMessageType<CustomModelPacket>();
    }

    private ICoreServerAPI? _api;
    private IServerNetworkChannel? _channel;
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class CustomModelPacket
{
    public string ModelName { get; set; } = "";
    public string Id { get; set; } = "";
    public byte[] Config { get; set; } = [];
    public byte[] Shape { get; set; } = [];
    public byte[] Texture { get; set; } = [];
    public byte[] Sounds { get; set; } = [];
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class CustomModelRequestPacket
{

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
    public void Add(CustomModelPacket packet)
    {
        string id = packet.Id;
        Shapes[id] = CreateAsset($"customplayermodel:shapes/{id}.json", packet.Shape, Origin);
        Textures[id] = CreateAsset($"customplayermodel:textures/{id}.png", packet.Texture, Origin);
        Sounds[id] = CreateAsset($"customplayermodel:sounds/{id}.ogg", packet.Sounds, Origin);
    }

    private Dictionary<string, IAsset> Shapes = [];
    private Dictionary<string, IAsset> Textures = [];
    private Dictionary<string, IAsset> Sounds = [];
    private SynchronizedDataOrigin Origin = new();

    private static IAsset CreateAsset(string path, byte[] data, SynchronizedDataOrigin origin)
    {
        Asset asset = new(data, path, origin);

        origin.Assets[asset] = data;

        return asset;
    }
}

public sealed class SynchronizedDataOrigin : IAssetOrigin
{
    public Dictionary<IAsset, byte[]> Assets { get; set; } = [];

    string IAssetOrigin.OriginPath => throw new NotImplementedException();
    List<IAsset> IAssetOrigin.GetAssets(AssetCategory category, bool shouldLoad) => throw new NotImplementedException();
    List<IAsset> IAssetOrigin.GetAssets(AssetLocation baseLocation, bool shouldLoad) => throw new NotImplementedException();
    bool IAssetOrigin.IsAllowedToAffectGameplay() => true;
    void IAssetOrigin.LoadAsset(IAsset asset) => throw new NotImplementedException();
    bool IAssetOrigin.TryLoadAsset(IAsset asset) => throw new NotImplementedException();
}