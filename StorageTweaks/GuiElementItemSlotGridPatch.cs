// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Cairo;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace StorageTweaks;

/// <summary>
/// Slot with item(s) in the favorite list
/// </summary>
public class FavoritedSlot
{
    private static LoadedTexture? _favoriteSlotTexture;
    private static IAsset? _favoriteSlotAsset;
    private static IAsset? _favoriteCornerAsset;
    private static ICoreClientAPI? _capi;
    private static readonly int FavoriteIconColor = ColorUtil.ColorFromRgba(181, 146, 118, 150);
    private static readonly int FavoriteIconOutlineColor = ColorUtil.ColorFromRgba(161, 129, 111, 150);

    private readonly ElementBounds _bounds;
    private readonly float _margin;
    private readonly float _iconSize;
    /// <summary>
    /// Slot with item(s) in the favorite list
    /// </summary>
    public FavoritedSlot(ElementBounds bounds)
    {
        _bounds = bounds;
        _margin = (float)GuiElement.scaled(0);
        _iconSize = (float)GuiElement.scaled(bounds.fixedWidth) - _margin * 2;
        EnsureIconTexture((int)_iconSize);
    }

    public void Draw()
    {
        if (_capi == null || _favoriteSlotTexture == null) return;
        var x = (float)(_bounds.renderX + _margin);
        var y = (float)(_bounds.renderY + _margin);
        _capi.Render.Render2DTexture(_favoriteSlotTexture.TextureId, _bounds, 50);
    }

    public static void SetApi(ICoreClientAPI api)
    {
        _capi = api;
    }

    // ReSharper disable once MemberCanBeMadeStatic.Local
    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    private void EnsureIconTexture(int size)
    {
        if (_capi == null) return;
        // if size hasn't changed don't re-render
        // if (_favoriteIconTexture?.Width == size) return;

        _favoriteCornerAsset = _capi.Assets.TryGet(new AssetLocation("storagetweaks", "textures/icons/favorite-slot-corner.svg"));
        _favoriteSlotAsset =
            _capi.Assets.TryGet(new AssetLocation("storagetweaks", "textures/icons/favorite-slot.svg"));
        if (_favoriteCornerAsset == null) return;

        _favoriteSlotTexture?.Dispose();
        _favoriteSlotTexture = new LoadedTexture(_capi);
        var surface = new ImageSurface(Format.Argb32, (int)GuiElement.scaled(_bounds.fixedWidth), (int)GuiElement.scaled(_bounds.fixedHeight));
        var ctx = new Context(surface);
        var cornerSize = (int)GuiElement.scaled(8);
        // draw a slightly upscaled version to act as an outline
        // _capi.Gui.DrawSvg(_favoriteIconAsset, surface, 2, 2, size - 3, size - 3, ColorUtil.ColorFromRgba(161, 129, 111, 80));
        var color = ColorUtil.ColorFromRgba(235, 235, 0, 50);
        // _capi.Gui.DrawSvg(_favoriteCornerAsset, surface, 2, 2, cornerSize, cornerSize, color);
        _capi.Gui.DrawSvg(_favoriteSlotAsset, surface, 0, 0, (int)GuiElement.scaled(_bounds.fixedWidth), (int)GuiElement.scaled(_bounds.fixedHeight), color);
        _capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref _favoriteSlotTexture);
        ctx.Dispose();
        surface.Dispose();
    }
}

[HarmonyPatch]
public class GuiElementItemSlotGridPatch
{
    public static bool HideFavorites
    {
        get => StorageTweaksModSystem.GetClientConfig().HideFavorites;
        set => StorageTweaksModSystem.GetClientConfig().HideFavorites = value;
    }

    private static readonly FieldInfo InventoryField =
        AccessTools.Field(typeof(GuiElementItemSlotGridBase), "inventory");

    [HarmonyPostfix, HarmonyPatch(typeof(GuiElementItemSlotGridBase), "RenderInteractiveElements")]
    public static void PostfixRenderInteractiveElements(
        // ReSharper disable once InconsistentNaming
        GuiElementItemSlotGridBase __instance,
        // ReSharper disable once UnusedParameter.Global
        float deltaTime)
    {
        var favoritesManager = FavoritesManager.Get();
        if (HideFavorites && !(favoritesManager?.IsFavoriteModeActive ?? HideFavorites)) return;
        if (favoritesManager == null) return;
        if (favoritesManager.GetFavoriteCount() == 0) return;

        var slotIndex = 0;
        foreach (KeyValuePair<int, ItemSlot> renderedSlot in __instance.renderedSlots)
        {
            if (slotIndex >= __instance.SlotBounds.Length) break;

            var value = renderedSlot.Value;
            if (value.Inventory is not (InventoryPlayerBackpacks or InventoryPlayerHotbar)) continue;
            if (value.Itemstack != null && favoritesManager.IsFavorite(value.Itemstack))
            {
                var elementBounds = __instance.SlotBounds[slotIndex];
                new FavoritedSlot(elementBounds).Draw();
            }

            slotIndex++;
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(GuiElementItemSlotGridBase), "SlotClick")]
    // ReSharper disable UnusedParameter.Global
    public static bool PrefixSlotClick(
        // ReSharper disable once InconsistentNaming
        GuiElementItemSlotGridBase __instance,
        ICoreClientAPI api,
        int slotId,
        EnumMouseButton mouseButton,
        bool shiftPressed,
        bool ctrlPressed,
        bool altPressed)
    {
        var favoritesManager = FavoritesManager.Get();
        if (favoritesManager is not { IsFavoriteModeActive: true }) return true;

        var inventory = (IInventory?)InventoryField.GetValue(__instance);
        if (inventory == null || slotId < 0 || slotId >= inventory.Count) return true;

        if (inventory is not (InventoryPlayerBackpacks or InventoryPlayerHotbar)) return true;

        var slot = inventory[slotId];
        if (slot?.Itemstack == null)
        {
            return false;
        }

        favoritesManager.ToggleFavorite(slot.Itemstack);
        api.Gui.PlaySound("tick");

        return false;
    }
}