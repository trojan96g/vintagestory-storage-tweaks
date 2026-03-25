// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
using HarmonyLib;
using Vintagestory.API.Client;

namespace StorageTweaks;

[HarmonyPatch(typeof(GuiComposerHelpers), "AddDialogTitleBar")]
public class GuiDialogBackpackInventoryPatch
{
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void Postfix(GuiComposer composer)
    {
        var capi = composer.Api;
        if (capi == null) return;
        
        if (composer.DialogName != "inventory-backpack") return;

        if (composer["storagetweaks-sort"] != null)
            return;

        PatchUtils.AddButton(composer, "sort", -60,
            inventory => PatchUtils.SendPacket(capi, new SortInventoryPacket { InventoryId = inventory.InventoryID }));
    }
}
