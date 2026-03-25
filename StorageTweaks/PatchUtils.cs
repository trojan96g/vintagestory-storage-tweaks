using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace StorageTweaks;

public static class PatchUtils
{
    public static void AddButton(GuiComposer composer, string type, int xOffset, Action<IInventory> onClick)
    {
        var capi = composer.Api;
        var iconAsset = new AssetLocation("storagetweaks", $"textures/icons/{type}.svg");
        var icon = capi.Assets.TryGet(iconAsset);
        if (icon == null) return;

        var bounds = ElementBounds.Fixed(EnumDialogArea.RightTop, xOffset, 4, 24, 24);
        var btn = new SvgButton(
            capi,
            icon,
            () =>
            {
                var inventory = GetInventoryForComposer(composer);
                if (inventory != null)
                {
                    onClick(inventory);
                }
                else
                {
                    capi.Logger.Debug(
                        "[StorageTweaks] {0} button clicked, but GetInventory returned null for composer {1}",
                        char.ToUpper(type[0]) + type[1..], composer.DialogName);
                }

                return true;
            },
            bounds
        );

        composer.AddInteractiveElement(btn, $"storagetweaks-{type}");
    }

    private static InventoryBase? GetInventoryForComposer(GuiComposer composer)
    {
        if (composer.DialogName == "inventory-backpack")
        {
            return composer.Api.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName) as InventoryBase;
        }
        var dialog = composer.Api.Gui.OpenedGuis.Find(d => d.Composers.Values.Any(c => c == composer));
        return dialog is GuiDialogBlockEntityInventory inventoryDialog ? inventoryDialog.Inventory : null;
    }

    public static void SendPacket<T>(ICoreClientAPI capi, T packet)
        where T : notnull
    {
        var channel = capi.Network.GetChannel("storagetweaks");
        if (channel != null)
        {
            channel.SendPacket(packet);
        }
        else
        {
            capi.Logger.Debug("[StorageTweaks] Failed to get network channel 'storagetweaks' for {0}",
                packet.GetType().Name);
        }
    }
}
