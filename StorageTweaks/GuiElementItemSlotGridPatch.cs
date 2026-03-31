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
    private static LoadedTexture? _favoriteIconTexture;
    private static IAsset? _favoriteIconAsset;
    private static ICoreClientAPI? _capi;
    private static readonly int FavoriteIconColor = ColorUtil.ColorFromRgba(181, 146, 118, 150);
    private static readonly int FavoriteIconOutlineColor = ColorUtil.ColorFromRgba(161, 129, 111, 150);

    private readonly ElementBounds _bounds;
    private readonly float _marginTop;
    private readonly float _marginLeft;
    private readonly float _iconSize;
    /// <summary>
    /// Slot with item(s) in the favorite list
    /// </summary>
    public FavoritedSlot(ElementBounds bounds)
    {
        _bounds = bounds;
        _marginLeft = (float)GuiElement.scaled(1);
        _marginTop = (float)GuiElement.scaled(1);
        _iconSize = (float)GuiElement.scaled(20);
        EnsureIconTexture((int)_iconSize);
    }

    public void Draw()
    {
        if (_capi == null || _favoriteIconTexture == null) return;
        var x = (float)(_bounds.renderX + _marginLeft);
        var y = (float)(_bounds.renderY + _marginTop);
        _capi.Render.Render2DTexture(_favoriteIconTexture.TextureId, x, y, _iconSize, _iconSize, 50);
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

        _favoriteIconAsset = _capi.Assets.TryGet(new AssetLocation("storagetweaks", "textures/icons/favorite-slot-corner.svg"));
        if (_favoriteIconAsset == null) return;

        _favoriteIconTexture?.Dispose();
        _favoriteIconTexture = new LoadedTexture(_capi);
        var surface = new ImageSurface(Format.Argb32, size, size);
        var ctx = new Context(surface);
        // draw a slightly upscaled version to act as an outline
        // _capi.Gui.DrawSvg(_favoriteIconAsset, surface, 2, 2, size - 3, size - 3, ColorUtil.ColorFromRgba(161, 129, 111, 80));
        _capi.Gui.DrawSvg(_favoriteIconAsset, surface, 2, 2, size - 4, size - 4, ColorUtil.ColorFromRgba(247, 250, 72, 150));
        _capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref _favoriteIconTexture);
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