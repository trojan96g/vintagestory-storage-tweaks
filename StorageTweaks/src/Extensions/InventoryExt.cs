using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace StorageTweaks.Extensions;

public static class InventoryExt
{
    public static List<ItemSlot> GetSortableSlots(this IInventory inventory, IServerPlayer fromPlayer, bool sortHotbarWithBackpack, bool skipFavorites)
    {
        var world = fromPlayer.Entity.World;

        // if sorting player backpack also include none favorite slots from hotbar in sorting
        var hotbarSlots = new List<ItemSlot>();
        var isPlayerBackpack = inventory.ClassName == GlobalConstants.backpackInvClassName;
        var hotbar = fromPlayer.InventoryManager.GetHotbarInventory();
        if (isPlayerBackpack && sortHotbarWithBackpack)
        {
            hotbarSlots =
            [
                .. hotbar.Where(s =>
                {
                    if (s.Empty)
                    {
                        return true;
                    }

                    // try catching here because one user got a null reference exception
                    // no idea how because s.Empty above should ensure that Itemstack is not null.
                    // The user that got the error actually had it happen in `UnloadInventory` but
                    // if it can happen there I imagine it can happen here too.
                    // https://mods.vintagestory.at/storagetweaks#cmt-193057
                    try
                    {
                        return !FavoritesManager.IsFavorite(fromPlayer, s.Itemstack);
                    }
                    catch (Exception e)
                    {
                        world.Logger.Error("[StorageTweaks] IsFavorite threw exception with item stack: {0}, {1}", s.Itemstack, s.Itemstack?.Collectible);
                        world.Logger.Error("[StorageTweaks] SortInventoryInternal: Exception {0}", e);
                        return false;
                    }
                }),
            ];
        }

        var slots = inventory.ToList();
        slots.AddRange(hotbarSlots);

        return
        [
            .. slots.Where(slot =>
            {
                if (!slot.CanSortMoveOut() || slot.Empty)
                {
                    return false;
                }

                if (!skipFavorites)
                {
                    return true;
                }

                try
                {
                    if (FavoritesManager.IsFavorite(fromPlayer, slot.Itemstack))
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    world.Logger.Error("[StorageTweaks] IsFavorite threw exception with item stack: {0}, {1}", slot.Itemstack, slot.Itemstack?.Collectible);
                    world.Logger.Error("[StorageTweaks] SortInventoryInternal: Exception {0}", e);
                }

                return true;
            }),
        ];
    }
}
