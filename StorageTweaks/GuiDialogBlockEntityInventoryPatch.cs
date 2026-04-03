// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

using HarmonyLib;
using Vintagestory.API.Client;
using System;
using System.Linq;

namespace StorageTweaks;

[HarmonyPatch(typeof(GuiComposerHelpers), "AddDialogTitleBar")]
public class GuiDialogBlockEntityInventoryPatch
{
    private static readonly string[] DialogNamePrefixes = ["blockentityinventory", "attachedcontainer"];
    
    [HarmonyPostfix]
    public static void Postfix(GuiComposer composer)
    {
        var capi = composer.Api;
        if (capi == null) return;

        if (composer.DialogName == null) return;

        if (!DialogNamePrefixes.Any(prefix => composer.DialogName.StartsWith(prefix, StringComparison.Ordinal))) return;

        if (composer["storagetweaks-sort"] != null || composer["storagetweaks-unload"] != null)
            return;

        PatchUtils.AddButton(composer, "sort", -60,
            inventory => PatchUtils.SendPacket(capi, new SortInventoryPacket { InventoryId = inventory.InventoryID }));

        PatchUtils.AddButton(composer, "unload", -86,
            inventory =>
                PatchUtils.SendPacket(capi, new UnloadInventoryPacket { InventoryId = inventory.InventoryID }));
    }
}