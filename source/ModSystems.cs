using PlayerModelLib;
using System.Diagnostics;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace CustomPlayerModel;

public sealed class CustomPlayerModelClientSystem : ModSystem
{
    public override double ExecuteOrder() => 0.22;

    public override void StartPre(ICoreAPI api)
    {
        _ = new AssetCategory("customplayermodels", AffectsGameplay: true, EnumAppSide.Client);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        _clientChannel = _clientApi.Network.RegisterChannel("CustomPlayerModel:model")
            .RegisterMessageType<CustomModelPacket>()
            .RegisterMessageType<CustomModelRequestPacket>()
            .SetMessageHandler<CustomModelPacket>(HandlePacketClient);
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

    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api is ICoreClientAPI clientApi)
        {
            LoadModelsFromMods(clientApi);
            LoadModelsFromFiles(clientApi);

            foreach ((_, CustomModelPacket data) in _modelsData)
            {
                _assetsManager.Add(data, api);
            }
        }
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

    private void LoadModelsFromFiles(ICoreClientAPI api)
    {
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

        string playerUid = GetPlayerId(api);

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
    }

    private string GetPlayerId(ICoreClientAPI api)
    {
        return api.Settings.String["playeruid"].Select(character => ((ushort)character).ToString()).Aggregate((f, s) => $"{f}{s}");
    }

    private void LoadModelsFromMods(ICoreClientAPI api)
    {
        List<AssetLocation> locations = api.Assets.GetLocations("customplayermodels");
        
        Dictionary<(string domain, string modelName), List<AssetLocation>> models = [];
        foreach (AssetLocation location in locations)
        {
            string domain = location.Domain;
            string path = location.Path;
            string modelName = Path.GetFileName(Path.GetDirectoryName(path));
            
            if (!models.ContainsKey((domain, modelName)))
            {
                models[(domain, modelName)] = [];
            }
            models[(domain, modelName)].Add(location);
        }

        string playerUid = GetPlayerId(api);

        foreach (((string domain, string modelName), List<AssetLocation> files) in models)
        {
            if (files.Count != 4)
            {
                LoggerUtil.Error(api, this, $"Model {domain}:{modelName} does not have 4 required files, or contains extra files");
                continue;
            }

            AssetLocation? configLocation = files.Find(loc => Path.GetExtension(loc.Path) == ".json" && Path.GetFileNameWithoutExtension(loc.Path) == "config");
            AssetLocation? shapeLocation = files.Find(loc => Path.GetExtension(loc.Path) == ".json" && Path.GetFileNameWithoutExtension(loc.Path) != "config");
            AssetLocation? textureLocation = files.Find(loc => Path.GetExtension(loc.Path) == ".png");
            AssetLocation? soundLocation = files.Find(loc => Path.GetExtension(loc.Path) == ".ogg");

            if (configLocation == null)
            {
                LoggerUtil.Error(api, this, $"Model {domain}:{modelName} does not contain config file");
                continue;
            }

            if (shapeLocation == null)
            {
                LoggerUtil.Error(api, this, $"Model {domain}:{modelName} does not contain shape file");
                continue;
            }

            if (textureLocation == null)
            {
                LoggerUtil.Error(api, this, $"Model {domain}:{modelName} does not contain texture file");
                continue;
            }

            if (soundLocation == null)
            {
                LoggerUtil.Error(api, this, $"Model {domain}:{modelName} does not contain sound file");
                continue;
            }

            string modelNameWithDomain = domain + "-" + modelName;

            CustomModelPacket modelData = new()
            {
                Id = Path.GetFileName(modelNameWithDomain.ToLower()) + "-" + playerUid + "-m",
                ModelName = Path.GetFileName(modelNameWithDomain.Replace('-', '_')),
                Config = api.Assets.Get(configLocation).Data,
                Shape = api.Assets.Get(shapeLocation).Data,
                Texture = api.Assets.Get(textureLocation).Data,
                Sound = api.Assets.Get(soundLocation).Data
            };
            _modelsData[modelData.Id] = modelData;

            modelData.Shape = Encoding.UTF8.GetBytes(Asset.BytesToString(modelData.Shape).Replace("\"custom-texture\"", $"\"customplayermodel:{modelData.Id}\""));
        }
    }

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
        config.Name ??= data.ModelName;

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
            Name = config.DisplayedName,
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