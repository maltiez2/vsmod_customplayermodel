using ConfigLib;
using PlayerModelLib;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace CustomPlayerModel;

public sealed class CustomPlayerModelClientSystem : ModSystem
{
    public Settings Settings { get; set; } = new();

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
            .RegisterMessageType<CustomModelWhiteList>()
            .SetMessageHandler<CustomModelPacket>(HandlePacketClient)
            .SetMessageHandler<CustomModelWhiteList>(HandlePacketClient);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _serverApi = api;
        _serverChannel = _serverApi.Network.RegisterChannel("CustomPlayerModel:model")
            .RegisterMessageType<CustomModelPacket>()
            .RegisterMessageType<CustomModelRequestPacket>()
            .RegisterMessageType<CustomModelWhiteList>()
            .SetMessageHandler<CustomModelPacket>(HandlePacketServer)
            .SetMessageHandler<CustomModelRequestPacket>(HandlePacketServer);

        _whiteList.Load(api);
        api.ModLoader.GetModSystem<CustomModelsSystem>().OnCustomModelChanged += OnCustomModelChanged;

        RegisterServerChatCommands(api);
    }

    public override void Start(ICoreAPI api)
    {
        if (api.ModLoader.IsModEnabled("configlib"))
        {
            SubscribeToConfigChange(api);
        }
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
    private readonly Dictionary<string, string> _playersPerModels = [];
    private readonly AssetsManager _assetsManager = new();
    private const string _domain = "customplayermodel";
    private ICoreClientAPI? _clientApi;
    private IClientNetworkChannel? _clientChannel;
    private ICoreServerAPI? _serverApi;
    private IServerNetworkChannel? _serverChannel;
    private readonly WhiteList _whiteList = new();

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
            _playersPerModels[modelData.Id] = playerUid;

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
            string? modelName = Path.GetFileName(Path.GetDirectoryName(path)) ?? "";

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
            _playersPerModels[modelData.Id] = playerUid;

            modelData.Shape = Encoding.UTF8.GetBytes(Asset.BytesToString(modelData.Shape).Replace("\"custom-texture\"", $"\"customplayermodel:{modelData.Id}\""));
        }
    }

    private static byte[] ReadFileIfExists(string path)
    {
        return File.Exists(path) ? File.ReadAllBytes(path) : [];
    }

    private void SendWhiteList(IServerPlayer player)
    {
        CustomModelWhiteList packet = new()
        {
            IsOnWhiteList = _whiteList.IsOnWhitelist(player),
            ModelsOnWhiteList = _modelsData.Values
                .Where(model => _whiteList.IsOnWhitelist(model, Settings))
                .Select(data => data.Id)
                .ToArray(),
            PlayersWhiteList = Settings.PlayersWhiteList,
            ModelsWhiteList = Settings.ModelsWhiteList
        };

        _serverChannel?.SendPacket(packet, player);
    }

    private void BroadcastWhiteList(ICoreServerAPI api)
    {
        foreach (IPlayer player in api.World.AllOnlinePlayers)
        {
            if (player is not IServerPlayer serverPlayer)
            {
                continue;
            }

            CustomModelWhiteList packet = new()
            {
                IsOnWhiteList = _whiteList.IsOnWhitelist(player),
                ModelsOnWhiteList = _modelsData.Values
                .Where(model => _whiteList.IsOnWhitelist(model, Settings))
                .Select(data => data.Id)
                .ToArray(),
                PlayersWhiteList = Settings.PlayersWhiteList,
                ModelsWhiteList = Settings.ModelsWhiteList
            };

            _serverChannel?.SendPacket(packet, serverPlayer);
        }
    }

    private void HandlePacketClient(CustomModelPacket packet)
    {
        if (_modelsData.ContainsKey(packet.Id) || _clientApi == null)
        {
            return;
        }

        _modelsData.Add(packet.Id, packet);
        _assetsManager.Add(packet, _clientApi);
        RegisterModel(_clientApi, packet.Id, packet, enabled: false);
    }

    private void HandlePacketClient(CustomModelWhiteList packet)
    {
        CustomModelsSystem? system = _clientApi?.ModLoader.GetModSystem<CustomModelsSystem>();
        if (system == null)
        {
            return;
        }

        foreach (string modelId in _modelsData.Values.Select(data => data.Id))
        {
            if (!system.CustomModels.TryGetValue(modelId, out CustomModelData? model))
            {
                continue;
            }

            if (!packet.IsOnWhiteList && packet.PlayersWhiteList)
            {
                model.Enabled = false;
            }
            else if (!packet.ModelsOnWhiteList.Contains(modelId) && packet.ModelsWhiteList && (packet.IsOnWhiteList || !packet.PlayersWhiteList))
            {
                model.Enabled = false;
            }
        }
    }

    private void HandlePacketServer(IServerPlayer player, CustomModelPacket packet)
    {
        if (_modelsData.ContainsKey(packet.Id) || _serverApi == null)
        {
            return;
        }

        _modelsData.Add(packet.Id, packet);
        _playersPerModels.Add(packet.Id, player.PlayerUID);
        _assetsManager.Add(packet, _serverApi);
        RegisterModel(_serverApi, packet.Id, packet, enabled: false);

        _serverChannel?.BroadcastPacket(packet, player);
    }

    private void HandlePacketServer(IServerPlayer player, CustomModelRequestPacket packet)
    {
        foreach ((_, CustomModelPacket? data) in _modelsData)
        {
            _serverChannel?.SendPacket(data, player);
        }
        SendWhiteList(player);
    }

    private void RegisterModel(ICoreAPI api, string id, CustomModelPacket data, bool enabled)
    {
        CustomModelsSystem system = api.ModLoader.GetModSystem<CustomModelsSystem>();

        string configText = Asset.BytesToString(data.Config);
        JsonObject json = JsonObject.FromJson(configText);
        CustomModelConfig config = GetConfig(id, json.AsObject<CustomModelConfig>(), new CustomModelConfig(), json["DisplayedName"]?.AsString(), Settings);
        config.Enabled = enabled;
        config.Name ??= data.ModelName;

        system.HotLoadCustomModel(id, config);
    }

    private static CustomModelConfig GetConfig(string id, CustomModelConfig config, CustomModelConfig defaultConfig, string? displayedName, Settings settings)
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

        config.Domain = _domain;
        config.Group = _domain;
        config.Name ??= displayedName;
        config.Icon = "customplayermodel:icons/model-icon";
        config.GroupIcon = "customplayermodel:icons/group-icon";
        config.ShapePath = $"{_domain}:shapes/{id}.json";
        config.SkinnableParts = skinnableParts;

        if (!settings.ConfigTraitsAndClass) config.ExtraTraits = defaultConfig.ExtraTraits;
        if (!settings.ConfigTraitsAndClass) config.AvailableClasses = defaultConfig.AvailableClasses;
        if (!settings.ConfigTraitsAndClass) config.ExclusiveClasses = defaultConfig.ExclusiveClasses;
        if (!settings.ConfigTraitsAndClass) config.SkipClasses = defaultConfig.SkipClasses;

        if (!settings.ConfigCollisionBox) config.CollisionBox = defaultConfig.CollisionBox;
        if (!settings.ConfigCollisionBox) config.MinCollisionBox = defaultConfig.MinCollisionBox;
        if (!settings.ConfigCollisionBox) config.MaxCollisionBox = defaultConfig.MaxCollisionBox;
        if (!settings.ConfigCollisionBox) config.ScaleColliderWithSizeHorizontally = defaultConfig.ScaleColliderWithSizeHorizontally;
        if (!settings.ConfigCollisionBox) config.ScaleColliderWithSizeVertically = defaultConfig.ScaleColliderWithSizeVertically;

        if (!settings.ConfigEyeHeight) config.EyeHeight = defaultConfig.EyeHeight;
        if (!settings.ConfigEyeHeight) config.MinEyeHeight = defaultConfig.MinEyeHeight;
        if (!settings.ConfigEyeHeight) config.MaxEyeHeight = defaultConfig.MaxEyeHeight;
        if (!settings.ConfigEyeHeight) config.SneakEyeHeightMultiplier = defaultConfig.SneakEyeHeightMultiplier;
        if (!settings.ConfigEyeHeight) config.SprintEyeHeightMultiplier = defaultConfig.SprintEyeHeightMultiplier;
        if (!settings.ConfigEyeHeight) config.WalkEyeHeightMultiplier = defaultConfig.WalkEyeHeightMultiplier;

        if (!settings.ConfigStepHeight) config.StepHeight = defaultConfig.StepHeight;
        if (!settings.ConfigSize) config.SizeRange = defaultConfig.SizeRange;
        if (!settings.ConfigSize) config.ModelSizeFactor = defaultConfig.ModelSizeFactor;
        if (!settings.ConfigOxygen) config.MaxOxygenFactor = defaultConfig.MaxOxygenFactor;

        return config;
    }

    private void OnCustomModelChanged(string model, IPlayer player, PlayerSkinBehavior skinBehavior)
    {
        CustomModelsSystem? system = _serverApi?.ModLoader.GetModSystem<CustomModelsSystem>();
        if (system == null)
        {
            return;
        }

        if (!_modelsData.TryGetValue(model, out CustomModelPacket? modelData))
        {
            return;
        }

        if (Settings.PlayersWhiteList && !_whiteList.IsOnWhitelist(player))
        {
            _serverApi?.World.RegisterCallback(_ =>
            {
                skinBehavior.SetCurrentModel(system.DefaultModelCode, system.DefaultModelData.SizeRange.X);
                player.Entity?.WatchedAttributes.SetBool("allowcharselonce", true);
                string message = $"Player '{player.PlayerName}' tried selecting custom model, but this player is not on white list. Player model was reset to default and player was granted 'allowcharselonce'.";
                LoggerUtil.Warn(_serverApi, this, message);
            }, 0);
            return;
        }

        if (!_whiteList.IsOnWhitelist(modelData, Settings) && !_whiteList.IsOnWhitelist(player))
        {
            _serverApi?.World.RegisterCallback(_ =>
            {
                skinBehavior.SetCurrentModel(system.DefaultModelCode, system.DefaultModelData.SizeRange.X);
                player.Entity?.WatchedAttributes.SetBool("allowcharselonce", true);
                string message = $"Player '{player.PlayerName}' tried selecting custom model, but this model is not on white list. Player model was reset to default and player was granted 'allowcharselonce'.";
                LoggerUtil.Warn(_serverApi, this, message);
            }, 0);
        }
    }

    private void SubscribeToConfigChange(ICoreAPI api)
    {
        ConfigLibModSystem system = api.ModLoader.GetModSystem<ConfigLibModSystem>();

        system.SettingChanged += (domain, config, setting) =>
        {
            if (domain != "customplayermodel") return;

            setting.AssignSettingValue(Settings);
        };

        system.ConfigsLoaded += () =>
        {
            system.GetConfig("customplayermodel")?.AssignSettingsValues(Settings);
        };
    }

    private void RegisterServerChatCommands(ICoreServerAPI api)
    {
        IChatCommandApi? chatCommandApi = api?.ChatCommands;
        CommandArgumentParsers? chatCommandParser = api?.ChatCommands.Parsers;

        chatCommandApi?
            .GetOrCreate("player")
            .BeginSub("cpmWhitelistAdd")
                .WithAlias("cpmwa")
                .RequiresPrivilege(Privilege.grantrevoke)
                .WithDesc("Add player to white list for selecting custom player models")
                .HandleWith((args) => CmdPlayer.Each(args, HandleCpmWhitelistAdd))
            .EndSub();

        chatCommandApi?
            .GetOrCreate("player")
            .BeginSub("cpmWhitelistRemove")
                .WithAlias("cpmwr")
                .RequiresPrivilege(Privilege.grantrevoke)
                .WithDesc("Remove player from white list for selecting custom player models")
                .HandleWith((args) => CmdPlayer.Each(args, HandleCpmWhitelistRemove))
            .EndSub();

        chatCommandApi?
            .GetOrCreate("player")
            .BeginSub("cpmWhitelistAddModel")
                .WithAlias("cpmwam")
                .WithArgs(new StringArgParser("model_name", true), new StringArgParser("flags", false))
                .RequiresPrivilege(Privilege.grantrevoke)
                .WithDesc("Add player model to white list")
                .HandleWith((args) => CmdPlayer.Each(args, HandleCpmWhitelistAddModel))
            .EndSub();

        chatCommandApi?
            .GetOrCreate("player")
            .BeginSub("cpmWhitelistRemoveModel")
                .WithAlias("cpmwrm")
                .WithArgs(new StringArgParser("model_name", true), new StringArgParser("flags", false))
                .RequiresPrivilege(Privilege.grantrevoke)
                .WithDesc("Remove player mode from white list")
                .HandleWith((args) => CmdPlayer.Each(args, HandleCpmWhitelistRemoveModel))
            .EndSub();
    }

    private TextCommandResult HandleCpmWhitelistAdd(PlayerUidName targetPlayer, TextCommandCallingArgs args)
    {
        ICoreServerAPI api = _serverApi ?? throw new ArgumentException("'cpmWhitelistAdd' should be run on server side");

        _whiteList.AddPlayer(api, targetPlayer.Uid, targetPlayer.Name);

        BroadcastWhiteList(api);

        return TextCommandResult.Success($"Player '{targetPlayer.Name}' now can select custom player models");
    }

    private TextCommandResult HandleCpmWhitelistRemove(PlayerUidName targetPlayer, TextCommandCallingArgs args)
    {
        ICoreServerAPI api = _serverApi ?? throw new ArgumentException("'cpmWhitelistRemove' should be run on server side");

        _whiteList.RemovePlayer(api, targetPlayer.Uid, targetPlayer.Name);

        BroadcastWhiteList(api);

        return TextCommandResult.Success($"Player '{targetPlayer.Name}' no longer can select custom player models");
    }

    private TextCommandResult HandleCpmWhitelistAddModel(PlayerUidName targetPlayer, TextCommandCallingArgs args)
    {
        ICoreServerAPI api = _serverApi ?? throw new ArgumentException("'cpmWhitelistAddModel' should be run on server side");

        string modelName = (((string?)args?.Parsers[1]?.GetValue()) ?? string.Empty).ToLowerInvariant();

        IEnumerable<CustomModelPacket> models = _modelsData
            .Where(entry => _playersPerModels.ContainsKey(entry.Key))
            .Where(entry => _playersPerModels[entry.Key] == targetPlayer.Uid)
            .Select(entry => entry.Value)
            .Where(entry => MatchModelName(api, entry.Id, entry.ModelName, modelName));

        if (!models.Any())
        {
            return TextCommandResult.Error($"No custom models with specified name found");
        }

        IEnumerable<char> flags = [];
        if (args?.Parsers.Count > 2)
        {
            flags = (((string?)args.Parsers[2]?.GetValue()) ?? string.Empty)?.ToLowerInvariant() ?? "";
        }

        if (!flags.Any())
        {
            models.Foreach(data => _whiteList.AddModel(api, data, targetPlayer.Name));
            BroadcastWhiteList(api);
            return TextCommandResult.Success($"Model '{modelName}' from player '{targetPlayer.Name}' was added to white list");
        }

        bool texture = flags.Contains('t');
        bool shape = flags.Contains('h');
        bool sound = flags.Contains('o');

        string result = "";

        if (texture)
        {
            models.Foreach(data => _whiteList.AddModelTexture(api, data, targetPlayer.Name));
            result += $"Texture from model '{modelName}' from player '{targetPlayer.Name}' was added to white list\n";
        }

        if (shape)
        {
            models.Foreach(data => _whiteList.AddModelShape(api, data, targetPlayer.Name));
            result += $"Shape from model '{modelName}' from player '{targetPlayer.Name}' was added to white list\n";
        }

        if (sound)
        {
            models.Foreach(data => _whiteList.AddModelSound(api, data, targetPlayer.Name));
            result += $"Sound from model '{modelName}' from player '{targetPlayer.Name}' was added to white list\n";
        }

        if (result != "")
        {
            BroadcastWhiteList(api);

            return TextCommandResult.Success(result);
        }
        else
        {
            return TextCommandResult.Error($"No valid flags specified. Allowed flags:\n  't' - texture\n  'h' - shape\n  'o' - sound");
        }
    }

    private TextCommandResult HandleCpmWhitelistRemoveModel(PlayerUidName targetPlayer, TextCommandCallingArgs args)
    {
        ICoreServerAPI api = _serverApi ?? throw new ArgumentException("'cpmWhitelistRemoveModel' should be run on server side");

        string modelName = (((string?)args?.Parsers[1]?.GetValue()) ?? string.Empty).ToLowerInvariant();

        IEnumerable<CustomModelPacket> models = _modelsData
            .Where(entry => _playersPerModels.ContainsKey(entry.Key))
            .Where(entry => _playersPerModels[entry.Key] == targetPlayer.Uid)
            .Select(entry => entry.Value)
            .Where(entry => MatchModelName(api, entry.Id, entry.ModelName, modelName));

        if (!models.Any())
        {
            return TextCommandResult.Error($"No custom models with specified name found from specified player found");
        }

        IEnumerable<char> flags = [];
        if (args?.Parsers.Count > 2)
        {
            flags = (((string?)args.Parsers[2]?.GetValue()) ?? string.Empty)?.ToLowerInvariant() ?? "";
        }

        if (!flags.Any())
        {
            models.Foreach(data => _whiteList.RemoveModel(api, data, targetPlayer.Name));
            BroadcastWhiteList(api);
            return TextCommandResult.Success($"Model '{modelName}' from player '{targetPlayer.Name}' was removed from white list");
        }

        bool texture = flags.Contains('t');
        bool shape = flags.Contains('h');
        bool sound = flags.Contains('o');

        string result = "";

        if (texture)
        {
            models.Foreach(data => _whiteList.RemoveModelTexture(api, data, targetPlayer.Name));
            result += $"Texture from model '{modelName}' from player '{targetPlayer.Name}' was removed from white list\n";
        }

        if (shape)
        {
            models.Foreach(data => _whiteList.RemoveModelShape(api, data, targetPlayer.Name));
            result += $"Shape from model '{modelName}' from player '{targetPlayer.Name}' was removed from white list\n";
        }

        if (sound)
        {
            models.Foreach(data => _whiteList.RemoveModelSound(api, data, targetPlayer.Name));
            result += $"Sound from model '{modelName}' from player '{targetPlayer.Name}' was removed from white list\n";
        }

        if (result != "")
        {
            BroadcastWhiteList(api);
            return TextCommandResult.Success(result);
        }
        else
        {
            return TextCommandResult.Error($"No valid flags specified. Allowed flags:\n  't' - texture\n  'h' - shape\n  'o' - sound");
        }
    }

    private bool MatchModelName(ICoreAPI api, string modelId, string modelName, string testString)
    {
        CustomModelsSystem? system = api.ModLoader.GetModSystem<CustomModelsSystem>();
        if (system == null)
        {
            return false;
        }

        string name = system.CustomModels[modelId].Name?.ToLowerInvariant() ?? "";
        modelName = modelName.ToLowerInvariant();
        testString = testString.ToLowerInvariant();

        return name == testString || modelName == testString;
    }
}