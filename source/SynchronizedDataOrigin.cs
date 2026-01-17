using Vintagestory.API.Common;

namespace CustomPlayerModel;

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
