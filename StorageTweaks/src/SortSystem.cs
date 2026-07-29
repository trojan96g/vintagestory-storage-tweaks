using System;
using System.Collections.Generic;
using System.Linq;
using StorageTweaks.Extensions;
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

        var isPlayerBackpack = inventory.ClassName == GlobalConstants.backpackInvClassName;
        var hotbar = fromPlayer.InventoryManager.GetHotbarInventory();

        var mergePriority = packet.StackPerishables ? EnumMergePriority.DirectMerge : EnumMergePriority.AutoMerge;

        var slots = inventory.GetSortableSlots(fromPlayer, packet.SortHotbarWithBackpack, packet.SkipFavoritesWhenSorting);

        try
        {
            // Compact stacks
            CompactStacks(slots, world, mergePriority);

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

                if (contentsComparison != 0)
                {
                    return contentsComparison;
                }

                // fruit tree cuttings are all the same except for having a different type attribute
                var typeA = a.Attributes.GetAsString("type") ?? "";
                var typeB = b.Attributes.GetAsString("type") ?? "";
                var typeComparison = string.Compare(typeA, typeB, StringComparison.Ordinal);

                return typeComparison != 0 ? typeComparison : b.StackSize.CompareTo(a.StackSize);
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
                    if (!weightedSlot.slot.CanSortInto())
                    {
                        world.Logger.Warning("Got best suited slot that is excluded: {0}",
                            weightedSlot.slot.GetType().Name);
                        continue;
                    }

                    sourceSlot.TryPutInto(weightedSlot.slot, ref op);
                }
            }

            // do a final compact on quivers and sheaths slots. These are excluded from sorting, but
            // we still want to compact them without moving them out of quivers and sheaths
            if (isPlayerBackpack)
            {
                var quiversAndSheaths = inventory.NonEmptyQuiversAndSheathsSlots();
                CompactStacks(quiversAndSheaths, world, mergePriority);
            }
        }
        catch (Exception e)
        {
            return new SortError($"Exception thrown while sorting: {e}");
        }

        return new SortSuccess();
    }

    private static void CompactStacks(IReadOnlyList<ItemSlot> slots, IWorldAccessor world, EnumMergePriority mergePriority)
    {
        for (var i = slots.Count - 1; i != 0; i--)
        {
            var sourceSlot = slots[i];

            var stack = sourceSlot.Itemstack;

            // Try to merge this stack into every other slot before this one
            for (var j = 0; j < i; j++)
            {
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
    }
}
