using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace StorageTweaks;

public abstract record SortResult;

public record SortError(string Message) : SortResult;

public record SortSuccess : SortResult;

public static class SortSystem
{
    public static void HandleSortInventory(IServerPlayer fromPlayer, SortInventoryPacket packet)
    {
        var inventory = fromPlayer.InventoryManager.GetInventory(packet.InventoryId);
        if (inventory == null)
        {
            return;
        }

        var world = fromPlayer.Entity.World;

        // Clone inventory for rollback on failure
        var snapshot = inventory.Select(s => s.Itemstack?.Clone()).ToList();

        var result = SortInventoryInternal(fromPlayer, inventory, packet);
        if (result is not SortError sortError)
        {
            return;
        }

        world.Logger.Fatal($"[StorageTweaks] Error in sort inventory: {sortError.Message}");
        world.Logger.Debug("[StorageTweaks] Attempting to rollback inventory");

        // crash if slots were somehow lost
        if (snapshot.Count != inventory.Count)
        {
            throw new Exception(
                $"[StorageTweaks] failed to restore inventory. Slot count mismatch. Snapshot slot count: {snapshot.Count}, inventory count: {inventory.Count}");
        }

        for (var i = 0; i < snapshot.Count; i++)
        {
            inventory[i].Itemstack = snapshot[i];
            inventory[i].MarkDirty();
        }

        world.Logger.Debug("[StorageTweaks] Finished rolling back inventory");

        const string message =
            "[StorageTweaks] Failed to sort inventory, inventory rolled back to previous state. Check server logs.";
        fromPlayer.SendIngameError("storagetweaks:rollback", message);
        fromPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, $"<font color=\"#ffea00\">{message}</font>",
            EnumChatType.CommandError);
    }

    private static SortResult SortInventoryInternal(IServerPlayer fromPlayer, IInventory inventory,
        SortInventoryPacket packet)
    {
        var world = fromPlayer.Entity.World;
        // we should probably add checks if the player is allowed to access the inventory

        var mergePriority = packet.StackPerishables ? EnumMergePriority.DirectMerge : EnumMergePriority.AutoMerge;

        // if sorting player backpack also include none favorite slots from hotbar in sorting
        var hotbarSlots = new List<ItemSlot>();
        var isPlayerBackpack = inventory.ClassName == GlobalConstants.backpackInvClassName;
        var hotbar = fromPlayer.InventoryManager.GetHotbarInventory();
        if (isPlayerBackpack && packet.SortHotbarWithBackpack)
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

        // Excludes specialized bag slots from sorting,
        // for example, Quivers And Sheaths item slots
        // Examples: ItemSlotBagContentWithWildcardMatch, ItemSlotTakeOutOnly
        slots = [.. slots.Where(slot =>
        {
            if (StorageTweaksModSystem.IsExcludedSlot(slot) || slot.Empty)
            {
                return false;
            }

            if (!packet.SkipFavoritesWhenSorting)
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
        })];

        try
        {
            // Compact stacks
            for (var i = 0; i < slots.Count; i++)
            {
                var sourceSlot = slots[i];

                var stack = sourceSlot.Itemstack;

                // Try to merge this stack into every other suitable slot
                for (var j = 0; j < slots.Count; j++)
                {
                    if (i == j)
                    {
                        continue; // Don't merge into itself
                    }

                    var targetSlot = slots[j];
                    if (targetSlot.Empty)
                    {
                        continue;
                    }

                    var op = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, mergePriority, stack.StackSize);
                    sourceSlot.TryPutInto(targetSlot, ref op);
                    if (sourceSlot.Empty)
                    {
                        break;
                    }
                }
            }

            // take out all stacks
            var itemStacks = slots.Where(s => !s.Empty).Select(x => x.TakeOutWhole()).ToList();

            // Sort by Class, Code, Contents and StackSize
            itemStacks.Sort((a, b) =>
            {
                var classComparison =
                    string.Compare(a.Collectible.Class, b.Collectible.Class, StringComparison.Ordinal);
                if (classComparison != 0)
                {
                    return classComparison;
                }

                var codeComparison = a.Collectible.Code.CompareTo(b.Collectible.Code);
                if (codeComparison != 0)
                {
                    return codeComparison;
                }

                var contentsA = a.Attributes.GetTreeAttribute("contents")?.ToJsonToken() ?? "";
                var contentsB = b.Attributes.GetTreeAttribute("contents")?.ToJsonToken() ?? "";
                var contentsComparison = string.Compare(contentsA, contentsB, StringComparison.Ordinal);

                return contentsComparison != 0 ? contentsComparison : b.StackSize.CompareTo(a.StackSize);
            });

            var skippedSlots = new List<ItemSlot>();
            // store the sorted stacks
            foreach (var stack in itemStacks)
            {
                skippedSlots.Clear();
                var sourceSlot = new DummySlot(stack);
                while (!sourceSlot.Empty && sourceSlot.Itemstack?.StackSize != 0)
                {
                    var op = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, mergePriority,
                        stack.StackSize);
                    var weightedSlot = inventory.GetBestSuitedSlot(sourceSlot,
                        op, skippedSlots);

                    if (weightedSlot.slot == null && isPlayerBackpack)
                    {
                        weightedSlot = hotbar.GetBestSuitedSlot(sourceSlot, op, skippedSlots);
                    }

                    if (weightedSlot.slot == null)
                    {
                        return new SortError("Failed to find a target slot to store stack");
                    }

                    skippedSlots.Add(weightedSlot.slot);
                    if (StorageTweaksModSystem.IsExcludedSlot(weightedSlot.slot))
                    {
                        world.Logger.Warning("Got best suited slot that is excluded: {0}",
                            weightedSlot.slot.GetType().Name);
                        continue;
                    }

                    sourceSlot.TryPutInto(weightedSlot.slot, ref op);
                }
            }


            foreach (var slot in slots)
            {
                slot.MarkDirty();
            }
        }
        catch (Exception e)
        {
            return new SortError($"Exception thrown while sorting: {e}");
        }

        return new SortSuccess();
    }
}
