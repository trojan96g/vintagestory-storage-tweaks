using StorageTweaks.Patches;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace StorageTweaks.Gui;

public class InventoryActionButtons
{
    private SvgToggleButton? favoriteToggleButton;
    private readonly StorageTweaksModSystem? modSystem;
    private readonly ICoreClientAPI capi;

    public InventoryActionButtons(ICoreClientAPI capi)
    {
        this.capi = capi;
        modSystem = capi.ModLoader.GetModSystem<StorageTweaksModSystem>();
        if (modSystem == null)
        {
            capi.Logger.Error("[StorageTweaks] StorageTweaksModSystem not found in InventoryActionButtons");
        }
    }

    public void ComposeGui(GuiComposer invComposer)
    {
        invComposer.Composed = false;
        capi.Logger.Debug("[StorageTweaks] Adding sort button");
        PatchUtils.AddButton(invComposer, "sort", -60,
            inventory => PatchUtils.SendPacket(capi, new SortInventoryPacket { InventoryId = inventory.InventoryID }),
            Lang.Get("storagetweaks:compact-and-sort"));

        capi.Logger.Debug("[StorageTweaks] Adding store-nearby button");
        PatchUtils.AddButton(invComposer, "store-nearby", -86,
            _ => PatchUtils.SendPacket(capi, new QuickStoreNearbyContainersPacket()),
            Lang.Get("storagetweaks:store-nearby"));

        capi.Logger.Debug("[StorageTweaks] Adding storagetweaks-favorite button");
        AddFavoriteToggle(invComposer);
        capi.Logger.Debug("[StorageTweaks] Adding storagetweaks-hide-favorites button");
        AddFavoritesHideToggle(invComposer);
    }

    private void AddFavoriteToggle(GuiComposer composer)
    {
        var iconAsset = new AssetLocation("storagetweaks", "textures/icons/favorite.svg");
        var icon = capi.Assets.TryGet(iconAsset);
        if (icon == null) return;

        var bounds = ElementBounds.Fixed(EnumDialogArea.RightTop, -112, 4, 24, 24);
        favoriteToggleButton = new SvgToggleButton(
            capi,
            icon,
            () => true,
            active => { modSystem!.FavoritesManager!.IsFavoriteModeActive = active; },
            bounds,
            ColorUtil.ColorFromRgba(247, 250, 72, 255),
            ColorUtil.ColorFromRgba(222, 225, 65, 255)
        );
        favoriteToggleButton.IsActive = modSystem?.FavoritesManager?.IsFavoriteModeActive ?? false;

        composer.AddInteractiveElement(favoriteToggleButton, "storagetweaks-favorite")
            .AddHoverText(
                Lang.Get("storagetweaks:toggle-favorite-mode-help"),
                CairoFont.WhiteSmallText(),
                250,
                bounds.FlatCopy()
            );
    }

    private void AddFavoritesHideToggle(GuiComposer composer)
    {
        var bounds = ElementBounds.Fixed(EnumDialogArea.RightTop, -138, 5, 24, 24);
        var toggleBtn = new GuiElementToggleButton(capi, null, "",
            CairoFont.SmallButtonText(), on => GuiElementItemSlotGridPatch.HideFavorites = on, bounds, true);
        toggleBtn.On = GuiElementItemSlotGridPatch.HideFavorites;
        composer.AddInteractiveElement(toggleBtn, "storagetweaks-hide-favorites").AddDynamicCustomDraw(bounds,
            (_, surface, _) =>
            {
                var iconAsset = new AssetLocation("storagetweaks", "textures/icons/favorites-hide.svg");
                var icon = capi.Assets.TryGet(iconAsset);
                var iconSize = (int)GuiElement.scaled(20.0);
                var margin = (int)GuiElement.scaled(2);
                if (icon != null)
                    capi.Gui.DrawSvg(icon, surface, margin, margin, iconSize, iconSize, SvgButton.NormalColor);
            }).AddHoverText(
            Lang.Get("storagetweaks:toggle-hide-favorites"), CairoFont.WhiteSmallText(), 250,
            bounds.FlatCopy()
        );
    }
}
