using StorageTweaks.Patches;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace StorageTweaks.Gui;

public class ContainerActionButtons(ICoreClientAPI capi)
{
    public void ComposeGui(GuiComposer composer)
    {
        var wasComposed = composer.Composed;
        composer.Composed = false;
        var config = StorageTweaksModSystem.GetClientConfig();
        var buttonIndex = 0;

        if (!config.HideSortButton)
        {
            PatchUtils.AddButton(composer, "sort", -60,
                inventory => PatchUtils.SendPacket(capi, new SortInventoryPacket
                {
                    InventoryId = inventory.InventoryID,
                    StackPerishables = config.StackPerishables,
                    SkipFavoritesWhenSorting = config.SkipFavoritesWhenSorting,
                }), Lang.Get("storagetweaks:compact-and-sort"));
            buttonIndex++;
        }

        if (!config.HideQuickStoreButton)
        {
            PatchUtils.AddButton(composer, "unload", -60 - buttonIndex * 26,
                inventory =>
                    PatchUtils.SendPacket(capi, new UnloadInventoryPacket
                    {
                        InventoryId = inventory.InventoryID,
                        StackPerishables = config.StackPerishables,
                    }), Lang.Get("storagetweaks:quick-store"));
        }

        if (wasComposed)
        {
            composer.Compose();
        }

        // With MoreInventorys mod the buttons appear 60px to the left of dialog.
        // Re-running CalcWorldBounds on each button fixes the alignment.
        foreach (var key in new[] { "storagetweaks-sort", "storagetweaks-unload" })
        {
            if (composer[key] is not { Bounds: { } btnBounds })
            {
                continue;
            }

            btnBounds.Initialized = false;
            btnBounds.CalcWorldBounds();
        }
    }
}
