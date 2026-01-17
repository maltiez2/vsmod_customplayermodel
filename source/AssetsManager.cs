using Vintagestory.API.Common;
using Vintagestory.Client;
using Vintagestory.Common;

namespace CustomPlayerModel;

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