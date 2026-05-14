using StorageTweaks.Patches;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace StorageTweaks.Gui;

public class ContainerActionButtons(ICoreClientAPI capi)
{
    public void ComposeGui(GuiComposer composer)
    {
        composer.Composed = false;
        PatchUtils.AddButton(composer, "sort", -60,
            inventory => PatchUtils.SendPacket(capi, new SortInventoryPacket { InventoryId = inventory.InventoryID }), Lang.Get("storagetweaks:compact-and-sort"));

        PatchUtils.AddButton(composer, "unload", -86,
            inventory =>
                PatchUtils.SendPacket(capi, new UnloadInventoryPacket { InventoryId = inventory.InventoryID }), Lang.Get("storagetweaks:quick-store"));

        composer.Compose();
    }
}
