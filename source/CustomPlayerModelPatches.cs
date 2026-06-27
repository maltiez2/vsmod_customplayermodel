using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.Server.Network;

namespace CustomPlayerModel;


public static class CustomPlayerModelPatches
{
    public static void Patch(string harmonyId)
    {
        new Harmony(harmonyId).Patch(
                typeof(TcpNetConnection).GetMethod("SetLengthLimit", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(CustomPlayerModelPatches), nameof(TcpNetConnection_SetLengthLimit)))
            );
    }

    public static void Unpatch(string harmonyId)
    {
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorTexturedClothing).GetMethod("reloadSkin", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
    }


    private static bool TcpNetConnection_SetLengthLimit(TcpNetConnection __instance, bool isCreative)
    {
        __instance.MaxPacketSize = isCreative ? int.MaxValue : CustomPlayerModelPatchesSystem.MaxPacketSize;
        return false;
    }
}

public class CustomPlayerModelPatchesSystem : ModSystem
{
    public static int MaxPacketSize { get; set; } = 2_000_000;

    public override void Start(ICoreAPI api)
    {
        CustomPlayerModelPatches.Patch(_harmonyId);
    }

    public override void Dispose()
    {
        CustomPlayerModelPatches.Unpatch(_harmonyId);
        base.Dispose();
    }

    private const string _harmonyId = "CustomPlayerModel";
}