// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using System;
using System.Collections.Generic;
using System.Reflection;
using Cairo;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace StorageTweaks.Patches;

/// <summary>
///     Slot with item(s) in the favorite list
/// </summary>
public class FavoritedSlot
{
    private static LoadedTexture? _favoriteIconTexture;
    private static ICoreClientAPI? _capi;
    private static readonly int FavoriteSlotCornerColor = ColorUtil.ColorFromRgba(250, 230, 51, 180);

    private readonly ElementBounds _bounds;
    private readonly float _iconSize;
    private readonly float _marginLeft;
    private readonly float _marginTop;

    /// <summary>
    ///     Slot with item(s) in the favorite list
    /// </summary>
    public FavoritedSlot(ElementBounds bounds)
    {
        _bounds = bounds;
        _marginLeft = (float)GuiElement.scaled(2);
        _marginTop = (float)GuiElement.scaled(2);
        _iconSize = (float)GuiElement.scaled(10);
        EnsureIconTexture((int)Math.Floor(_iconSize));
        return;

        static void EnsureIconTexture(int size)
        {
            if (_capi == null) return;
            // if size hasn't changed, don't re-render
            if (_favoriteIconTexture?.Width == size) return;

            var favoriteIconAsset =
                _capi.Assets.TryGet(new AssetLocation("storagetweaks", "textures/icons/favorite-slot-corner.svg"));
            if (favoriteIconAsset == null) return;

            _favoriteIconTexture?.Dispose();
            _favoriteIconTexture = new LoadedTexture(_capi);
            var surface = new ImageSurface(Format.Argb32, size, size);
            var ctx = new Context(surface);
            _capi.Gui.DrawSvg(favoriteIconAsset, surface, 0, 0, size, size, FavoriteSlotCornerColor);
            _capi.Gui.LoadOrUpdateCairoTexture(surface, false, ref _favoriteIconTexture);
            ctx.Dispose();
            surface.Dispose();
        }
    }

    public void Draw()
    {
        if (_capi == null || _favoriteIconTexture == null) return;

        var x = (float)Math.Round(_bounds.renderX + _marginLeft, MidpointRounding.AwayFromZero);
        var y = (float)Math.Round(_bounds.renderY + _marginTop, MidpointRounding.AwayFromZero);
        _capi.Render.Render2DTexture(_favoriteIconTexture.TextureId, x, y, (float)Math.Floor(_iconSize),
            (float)Math.Floor(_iconSize));
    }

    public static void SetApi(ICoreClientAPI api)
    {
        _capi = api;
    }
}

[HarmonyPatch]
public class GuiElementItemSlotGridPatch
{
    private static readonly FieldInfo InventoryField =
        AccessTools.Field(typeof(GuiElementItemSlotGridBase), "inventory");

    public static bool HideFavorites
    {
        get => StorageTweaksModSystem.GetClientConfig().HideFavorites;
        set => StorageTweaksModSystem.GetClientConfig().HideFavorites = value;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElementItemSlotGridBase), "RenderInteractiveElements")]
    public static void PostfixRenderInteractiveElements(
        // ReSharper disable once InconsistentNaming
        GuiElementItemSlotGridBase __instance,
        // ReSharper disable once UnusedParameter.Global
        float deltaTime)
    {
        var favoritesManager = FavoritesManager.Get();
        if (HideFavorites && !(favoritesManager?.IsFavoriteModeActive ?? HideFavorites)) return;
        if (favoritesManager == null) return;

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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GuiElementItemSlotGridBase), "SlotClick")]
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
        if (slot?.Itemstack == null) return false;

        favoritesManager.ToggleFavorite(slot.Itemstack);
        api.Gui.PlaySound("tick");

        return false;
    }
}