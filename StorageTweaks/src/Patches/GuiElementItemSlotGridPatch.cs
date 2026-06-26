// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using System;
using System.Reflection;
using Cairo;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace StorageTweaks.Patches;

/// <summary>
///     Slot with item(s) in the favorite list
/// </summary>
public class FavoritedSlot
{
    private static LoadedTexture? favoriteIconTexture;
    private static readonly int FavoriteSlotCornerColor = ColorUtil.ColorFromRgba(250, 230, 51, 180);

    private readonly ElementBounds bounds;
    private readonly ICoreClientAPI capi;
    private readonly float iconSize;
    private readonly float marginLeft;
    private readonly float marginTop;

    /// <summary>
    ///     Slot with item(s) in the favorite list
    /// </summary>
    public FavoritedSlot(ElementBounds bounds, ICoreClientAPI api)
    {
        capi = api;
        this.bounds = bounds;
        marginLeft = (float)GuiElement.scaled(2);
        marginTop = (float)GuiElement.scaled(2);
        iconSize = (float)GuiElement.scaled(10);
        EnsureIconTexture((int)Math.Floor(iconSize), capi);
        return;

        static void EnsureIconTexture(int size, ICoreClientAPI capi)
        {
            // if size hasn't changed, don't re-render
            if (favoriteIconTexture?.Width == size)
            {
                return;
            }

            var favoriteIconAsset =
                capi.Assets.TryGet(new AssetLocation("storagetweaks", "textures/icons/favorite-slot-corner.svg"));
            if (favoriteIconAsset == null)
            {
                return;
            }

            favoriteIconTexture?.Dispose();
            favoriteIconTexture = new LoadedTexture(capi);
            var surface = new ImageSurface(Format.Argb32, size, size);
            var ctx = new Context(surface);
            capi.Gui.DrawSvg(favoriteIconAsset, surface, 0, 0, size, size, FavoriteSlotCornerColor);
            capi.Gui.LoadOrUpdateCairoTexture(surface, false, ref favoriteIconTexture);
            ctx.Dispose();
            surface.Dispose();
        }
    }

    public void Draw()
    {
        if (favoriteIconTexture == null)
        {
            return;
        }

        var x = (float)Math.Round(bounds.renderX + marginLeft, MidpointRounding.AwayFromZero);
        var y = (float)Math.Round(bounds.renderY + marginTop, MidpointRounding.AwayFromZero);
        capi.Render.Render2DTexture(favoriteIconTexture.TextureId, x, y, (float)Math.Floor(iconSize),
            (float)Math.Floor(iconSize));
    }
}

[HarmonyPatch]
public class GuiElementItemSlotGridPatch
{
    private static readonly FieldInfo InventoryField =
        AccessTools.Field(typeof(GuiElementItemSlotGridBase), "inventory");

    private static readonly FieldInfo RenderedSlotsField =
        AccessTools.Field(typeof(GuiElementItemSlotGridBase), "renderedSlots");

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
        var capi = GetApi(__instance);
        var favoritesManager = GetFavoritesManager(__instance, "PostfixRenderInteractiveElements");
        if (HideFavorites && !(favoritesManager?.IsFavoriteModeActive ?? HideFavorites))
        {
            return;
        }

        if (favoritesManager == null)
        {
            return;
        }

        var slotIndex = 0;
        var renderedSlots =
            (OrderedDictionary<int, ItemSlot>)RenderedSlotsField.GetValue(__instance)!;
        foreach (var renderedSlot in renderedSlots)
        {
            if (slotIndex >= __instance.SlotBounds.Length)
            {
                break;
            }

            var value = renderedSlot.Value;
            if (!(Util.IsPlayerBackpack(value.Inventory) || Util.IsPlayerHotbar(value.Inventory)))
            {
                continue;
            }

            if (value.Itemstack != null && favoritesManager.IsFavorite(value.Itemstack))
            {
                var elementBounds = __instance.SlotBounds[slotIndex];
                new FavoritedSlot(elementBounds, capi).Draw();
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
        var favoritesManager = GetFavoritesManager(__instance, "PrefixSlotClick");
        if (favoritesManager is not { IsFavoriteModeActive: true })
        {
            return true;
        }

        var inventory = (IInventory?)InventoryField.GetValue(__instance);
        if (inventory == null || slotId < 0 || slotId >= inventory.Count)
        {
            return true;
        }

        if (!(Util.IsPlayerBackpack(inventory) || Util.IsPlayerHotbar(inventory)))
        {
            return true;
        }

        var slot = inventory[slotId];
        if (slot?.Itemstack == null)
        {
            return false;
        }

        favoritesManager.ToggleFavorite(slot.Itemstack);
        api.Gui.PlaySound("tick");

        return false;
    }

    private static ICoreClientAPI GetApi(GuiElementItemSlotGridBase instance)
    {
        var field = instance.GetType().GetField("api", BindingFlags.NonPublic | BindingFlags.Instance);
        return (ICoreClientAPI)field!.GetValue(instance)!;
    }

    private static FavoritesManager? GetFavoritesManager(GuiElementItemSlotGridBase instance, string context)
    {
        var capi = GetApi(instance);
        var modSystem = capi.ModLoader.GetModSystem<StorageTweaksModSystem>();
        if (modSystem == null)
        {
            capi.Logger.Warning(
                "[StorageTweaks] Failed to get StorageTweaksModSystem in GuiElementItemSlotGridPatch::{0}", context);
            return null;
        }

        var favoritesManager = modSystem.FavoritesManager;
        if (favoritesManager == null)
        {
            capi.Logger.Warning("[StorageTweaks] Failed to get favorites manager in  GuiElementItemSlotGridPatch::{0}",
                context);
        }

        return favoritesManager;
    }
}
