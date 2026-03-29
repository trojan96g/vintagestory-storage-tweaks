// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

using HarmonyLib;
using Vintagestory.API.Client;
using System;

namespace StorageTweaks;

[HarmonyPatch(typeof(GuiComposerHelpers), "AddDialogTitleBar")]
public class GuiDialogBlockEntityInventoryPatch
{
    [HarmonyPostfix]
    public static void Postfix(GuiComposer composer)
    {
        var capi = composer.Api;
        if (capi == null) return;

        if (composer.DialogName == null) return;

        if (!composer.DialogName.StartsWith("blockentityinventory", StringComparison.OrdinalIgnoreCase)) return;

        if (composer["storagetweaks-sort"] != null || composer["storagetweaks-unload"] != null)
            return;

        PatchUtils.AddButton(composer, "sort", -60,
            inventory => PatchUtils.SendPacket(capi, new SortInventoryPacket { InventoryId = inventory.InventoryID }));

        PatchUtils.AddButton(composer, "unload", -86,
            inventory =>
                PatchUtils.SendPacket(capi, new UnloadInventoryPacket { InventoryId = inventory.InventoryID }));
    }
}