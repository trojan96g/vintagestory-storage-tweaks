using HarmonyLib;
using Vintagestory.API.Client;

namespace StorageTweaks.Patches;

/// <summary>
///     Dynamic patch for MoreInventorys mod. Applied if the MoreInventorys mod is present.
///
///     Using <see cref="AccessTools"/> to patch the mod without directly depending on it.
/// </summary>
public static class MoreInventorysPatch
{
    private const string CrateDialogType = "MoreInventorys.src.GuiFolder.GuiDialogCrateClosed";
    private const string DynamicDialogType = "MoreInventorys.src.GuiFolder.GuiDialogDynamic";
    private const string SetupDialogMethod = "SetupDialog";

    /// <summary>
    ///     Patches the More Inventorys SetupDialog methods if the mod
    ///     is present. Subsequent calls skip already-patched methods.
    /// </summary>
    public static void Apply(Harmony harmony, ICoreClientAPI capi)
    {
        PatchOne(harmony, capi, CrateDialogType);
        PatchOne(harmony, capi, DynamicDialogType);
    }

    private static void PatchOne(Harmony harmony, ICoreClientAPI capi, string typeName)
    {
        var type = AccessTools.TypeByName(typeName);
        if (type == null)
        {
            capi.Logger.Debug("[StorageTweaks] More Inventorys type {0} not loaded; skipping patch",
                typeName);
            return;
        }

        var method = AccessTools.Method(type, SetupDialogMethod);
        if (method == null)
        {
            capi.Logger.Warning("[StorageTweaks] More Inventorys {0}.{1} not found; cannot refresh buttons on slot change",
                typeName, SetupDialogMethod);
            return;
        }

        var postfix = new HarmonyMethod(typeof(MoreInventorysPatch), nameof(SetupDialogPostfix));
        harmony.Patch(method, postfix: postfix);
        capi.Logger.Debug("[StorageTweaks] Patched {0}.{1} for button refresh on slot change",
            typeName, SetupDialogMethod);
    }

    // ReSharper disable once InconsistentNaming
    private static void SetupDialogPostfix(GuiDialog __instance)
    {
        GuiDialogBlockEntityInventoryPatch.OnGuiDialogOpened(__instance);
    }
}