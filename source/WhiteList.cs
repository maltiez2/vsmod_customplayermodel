using PlayerModelLib;
using System.Security.Cryptography;
using Vintagestory.API.Common;

namespace CustomPlayerModel;

public class WhiteList
{
    public HashSet<string> Players { get; set; } = [];
    public HashSet<string> Shapes { get; set; } = [];
    public HashSet<string> Textures { get; set; } = [];
    public HashSet<string> Sounds { get; set; } = [];
    public List<string> HashLog { get; set; } = [];

    public void Load(ICoreAPI api)
    {
        WhiteList list = api.LoadModConfig<WhiteList>(_whiteListFile) ?? new();
        Players = list.Players;
        Shapes = list.Shapes;
        Textures = list.Textures;
        Sounds = list.Sounds;
        HashLog = list.HashLog;
    }

    public void Store(ICoreAPI api)
    {
        HashLog = HashLog.OrderDescending().ToList();
        api.StoreModConfig(this, _whiteListFile);
    }

    public static string GetHash(byte[] data)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public bool IsOnWhitelist(CustomModelPacket modelData, Settings settings)
    {
        string shapeHash = GetHash(modelData.Shape);
        string textureHash = GetHash(modelData.Texture);
        string soundHash = GetHash(modelData.Sound);

        bool shape = Shapes.Contains(shapeHash) || !(settings.ModelsWhiteList || settings.ShapesWhiteList);
        bool texture = Textures.Contains(textureHash) || !(settings.ModelsWhiteList || settings.TexturesWhiteList);
        bool sound = Sounds.Contains(soundHash) || !(settings.ModelsWhiteList || settings.SoundsWhiteList);

        return shape && texture && sound;
    }

    public bool IsOnWhitelist(IPlayer player)
    {
        string? playerUid = player.PlayerUID;
        if (playerUid == null)
        {
            return false;
        }

        return Players.Contains(playerUid);
    }

    public void AddModel(ICoreAPI api, CustomModelPacket modelData, string player)
    {
        string shapeHash = GetHash(modelData.Shape);
        string textureHash = GetHash(modelData.Texture);
        string soundHash = GetHash(modelData.Sound);

        Shapes.Add(shapeHash);
        Textures.Add(textureHash);
        Sounds.Add(soundHash);

        AddHashLogEntry(shapeHash, "shape", player);
        AddHashLogEntry(textureHash, "texture", player);
        AddHashLogEntry(soundHash, "sound", player);

        LoggerUtil.Notify(api, this, $"Added texture, shape and sound to white list from model '{modelData.ModelName}' from player '{player}'");

        Store(api);
    }

    public void AddModelShape(ICoreAPI api, CustomModelPacket modelData, string player)
    {
        string shapeHash = GetHash(modelData.Shape);

        Shapes.Add(shapeHash);

        AddHashLogEntry(shapeHash, "shape", player);

        LoggerUtil.Notify(api, this, $"Added shape to white list from model '{modelData.ModelName}' from player '{player}'");

        Store(api);
    }

    public void AddModelTexture(ICoreAPI api, CustomModelPacket modelData, string player)
    {
        string textureHash = GetHash(modelData.Texture);

        Textures.Add(textureHash);

        AddHashLogEntry(textureHash, "texture", player);

        LoggerUtil.Notify(api, this, $"Added texture to white list from model '{modelData.ModelName}' from player '{player}'");

        Store(api);
    }

    public void AddModelSound(ICoreAPI api, CustomModelPacket modelData, string player)
    {
        string soundHash = GetHash(modelData.Sound);

        Sounds.Add(soundHash);

        AddHashLogEntry(soundHash, "sound", player);

        LoggerUtil.Notify(api, this, $"Added sound to white list from model '{modelData.ModelName}' from player '{player}'");

        Store(api);
    }

    public void RemoveModel(ICoreAPI api, CustomModelPacket modelData, string player)
    {
        string shapeHash = GetHash(modelData.Shape);
        string textureHash = GetHash(modelData.Texture);
        string soundHash = GetHash(modelData.Sound);

        Shapes.Remove(shapeHash);
        Textures.Remove(textureHash);
        Sounds.Remove(soundHash);

        LoggerUtil.Notify(api, this, $"Removed texture, shape and sound from white list from model '{modelData.ModelName}' from player '{player}'");

        Store(api);
    }

    public void RemoveModelShape(ICoreAPI api, CustomModelPacket modelData, string player)
    {
        string shapeHash = GetHash(modelData.Shape);

        Shapes.Remove(shapeHash);

        LoggerUtil.Notify(api, this, $"Removed shape from white list from model '{modelData.ModelName}' from player '{player}'");

        Store(api);
    }

    public void RemoveModelTexture(ICoreAPI api, CustomModelPacket modelData, string player)
    {
        string textureHash = GetHash(modelData.Texture);

        Textures.Remove(textureHash);

        LoggerUtil.Notify(api, this, $"Removed texture from white list from model '{modelData.ModelName}' from player '{player}'");

        Store(api);
    }

    public void RemoveModelSound(ICoreAPI api, CustomModelPacket modelData, string player)
    {
        string soundHash = GetHash(modelData.Sound);

        Sounds.Remove(soundHash);

        LoggerUtil.Notify(api, this, $"Removed sound from white list from model '{modelData.ModelName}' from player '{player}'");

        Store(api);
    }

    public void AddPlayer(ICoreAPI api, IPlayer player)
    {
        string? playerUid = player.PlayerUID;
        if (playerUid == null)
        {
            return;
        }

        Players.Add(playerUid);

        LoggerUtil.Notify(api, this, $"Added player '{player.PlayerName}' to white list");

        Store(api);
    }

    public void AddPlayer(ICoreAPI api, string uid, string playerName)
    {
        Players.Add(uid);

        LoggerUtil.Notify(api, this, $"Added player '{playerName}' to white list");

        Store(api);
    }

    public void RemovePlayer(ICoreAPI api, IPlayer player)
    {
        string? playerUid = player.PlayerUID;
        if (playerUid == null)
        {
            return;
        }

        Players.Remove(playerUid);

        LoggerUtil.Notify(api, this, $"Removed player '{player.PlayerName}' from white list");

        Store(api);
    }

    public void RemovePlayer(ICoreAPI api, string uid, string playerName)
    {
        Players.Remove(uid);

        LoggerUtil.Notify(api, this, $"Removed player '{playerName}' from white list");

        Store(api);
    }

    
    
    private const string _whiteListFile = "custom-player-models-white-list.json";

    private void AddHashLogEntry(string hash, string type, string playerName)
    {
        string timeStamp = DateTime.Now.ToString("yy-MM-dd-HH-mm-ss");
        string entry = $"{timeStamp}    {type}    {playerName}    {hash}";
        HashLog.Add(entry);
    }
}