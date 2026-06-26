// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

using System;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;

// ReSharper disable UnusedParameter.Global

namespace StorageTweaks.Patches;

[HarmonyPatch]
public static class GuiDialogBlockEntityInventoryPatch
{
    public static readonly string[] DialogNamePrefixes = ["blockentityinventory", "attachedcontainer"];

    [HarmonyPatch(typeof(GuiDialog), "OnGuiOpened")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once MemberCanBePrivate.Global
    private static void OnGuiDialogOpened(GuiDialog __instance)
    {
        var capi = GuiDialogInventoryPatch.GetApi(__instance);
        var composer = __instance.SingleComposer;
        if (composer?.DialogName == null)
        {
            return;
        }

        // already added
        var storageTweaksKeys = new[] { "storagetweaks-sort", "storagetweaks-unload" };
        if (storageTweaksKeys.Any(key => composer[key] != null))
        {
            return;
        }

        if (!DialogNamePrefixes.Any(prefix => composer.DialogName.StartsWith(prefix, StringComparison.Ordinal)))
        {
            capi.Logger.Debug("[StorageTweaks] {0} not in whitelist for block entity dialog names",
                composer.DialogName);
            return;
        }

        var modSystem = capi.ModLoader.GetModSystem<StorageTweaksModSystem>();

        if (modSystem == null)
        {
            capi.Logger.Warning("[StorageTweaks] StorageTweaksModSystem not found");
            return;
        }

        modSystem.ContainerActionButtons!.ComposeGui(composer);
    }


    [HarmonyPatch(typeof(GuiDialogBlockEntityInventory), "OnGuiOpened")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void OnGuiDialogBlockEntityInventoryOpened(GuiDialogBlockEntityInventory __instance)
    {
        OnGuiDialogOpened(__instance);
    }
}
