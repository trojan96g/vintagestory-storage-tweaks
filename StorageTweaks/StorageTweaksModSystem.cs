using System;
using Vintagestory.API.Config;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using ProtoBuf;
using Vintagestory.Common;

namespace StorageTweaks;

[ProtoContract]
public class SortInventoryPacket
{
    [ProtoMember(1)] public required string InventoryId;
}

[ProtoContract]
public class UnloadInventoryPacket
{
    [ProtoMember(1)] public required string InventoryId;
}

// ReSharper disable once UnusedType.Global
public class StorageTweaksModSystem : ModSystem
{
    private Harmony? _harmony;

    // a list of quality foods and tools to exclude from automatic unloading
    private static readonly HashSet<string> ToolAndFoodCodes = [];

    public override bool ShouldLoad(EnumAppSide forSide) => true;


    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        _harmony = new Harmony("storagetweaks");
        _harmony.PatchAll(typeof(StorageTweaksModSystem).Assembly);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Network.RegisterChannel("storagetweaks")
            .RegisterMessageType<SortInventoryPacket>()
            .RegisterMessageType<UnloadInventoryPacket>();
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Network.RegisterChannel("storagetweaks")
            .RegisterMessageType<SortInventoryPacket>()
            .RegisterMessageType<UnloadInventoryPacket>()
            .SetMessageHandler<SortInventoryPacket>(HandleSortInventory)
            .SetMessageHandler<UnloadInventoryPacket>(HandleUnloadInventory);

        PopulateToolAndFoodCodes(api);
    }

    private static void PopulateToolAndFoodCodes(ICoreAPI api)
    {
        ToolAndFoodCodes.Clear();

        var keywords = new[]
        {
            "axe",
            "knife",
            "pickaxe",
            "pie",
            "tongs",
            "arrow",
            "bow",
            "bowl",
            "cleaver",
            "cookingpot",
            "crock",
            "falx",
            "hammer",
            "hoe",
            "lantern",
            "saw",
            "scythe",
            "shears",
            "shield",
            "shovel",
            "spear",
            "sword",
            "torch"
        };
        var excludeKeywords = new[]
        {
            "blade", "part", "raw", "stackrandomizer", "toolmold", "-down", "-north", "-east", "-south", "-west"
        };

        foreach (var collectible in api.World.Items.Concat(api.World.Collectibles))
        {
            if (collectible.Code == null) continue;

            var code = collectible.Code.ToString();
            var parts = code.Split(':', '-');

            // Check if any part matches a keyword
            if (!keywords.Any(k => parts.Any(p => string.Equals(p, k, StringComparison.OrdinalIgnoreCase)))) continue;

            // Exclude unwanted items
            if (excludeKeywords.Any(k => code.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;


            ToolAndFoodCodes.Add(code);
        }

        api.World.Logger.Debug("[StorageTweaks] Populated {0} tool and food codes.", ToolAndFoodCodes.Count);
    }

    private static void HandleSortInventory(IServerPlayer fromPlayer, SortInventoryPacket packet)
    {
        var inventory = fromPlayer.InventoryManager.GetInventory(packet.InventoryId);
        if (inventory == null) return;

        SortInventoryInternal(fromPlayer.Entity.World, inventory);
    }

    private static void HandleUnloadInventory(IServerPlayer fromPlayer, UnloadInventoryPacket packet)
    {
        // should probably add checks if the player is allowed to access the inventory

        var logger = fromPlayer.Entity.World.Logger;
        var destInventory = fromPlayer.InventoryManager.GetInventory(packet.InventoryId);
        if (destInventory == null)
        {
            logger.Debug(
                "[StorageTweaks] HandleUnloadInventory: Destination inventory not found");
            return;
        }

        var playerInv = fromPlayer.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (playerInv == null)
        {
            logger.Debug(
                "[StorageTweaks] HandleUnloadInventory: Player backpack inventory not found");
            return;
        }

        var playerHotbar = fromPlayer.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
        if (playerHotbar == null)
        {
            logger.Debug(
                "[StorageTweaks] HandleUnloadInventory: Player hotbar inventory not found");
            return;
        }

        // // for debugging
        // if (fromPlayer.Entity.World.Api is ICoreServerAPI serverApi)
        // {
        //     PopulateToolAndFoodCodes(serverApi);
        // }
        // logger.Debug("[StorageTweaks] exclusion codes:\n{0}",
        //     string.Join("\n", ToolAndFoodCodes));

        // // for debugging
        // foreach (var slot in playerHotbar)
        // {
        //     if (slot.Empty) continue;
        //     logger.Debug("[StorageTweaks] hotbar slot item: {0}",
        //         slot.Itemstack.Collectible.Code);
        // }

        var existingCodes = new HashSet<string>();
        foreach (var destSlot in destInventory)
        {
            if (destSlot.Empty) continue;
            if (ToolAndFoodCodes.Contains(destSlot.Itemstack.Collectible.Code.ToString())) continue;
            existingCodes.Add(destSlot.Itemstack.Collectible.Code.ToString());
        }

        if (existingCodes.Count == 0)
        {
            return;
        }

        ProcessInventorySlots(playerInv, destInventory, existingCodes, fromPlayer);
        ProcessInventorySlots(playerHotbar, destInventory, existingCodes, fromPlayer);
    }

    private static void ProcessInventorySlots(IInventory sourceInventory, IInventory destInventory,
        HashSet<string> existingCodes, IServerPlayer fromPlayer)
    {
        foreach (var slot in sourceInventory)
        {
            if (slot.Empty) continue;
            if (!existingCodes.Contains(slot.Itemstack.Collectible.Code.ToString())) continue;

            var world = fromPlayer.Entity.World;
            var suitedSlot = destInventory.GetBestSuitedSlot(slot);
            while (suitedSlot != null && suitedSlot.weight != 0)
            {
                slot.TryPutInto(world, suitedSlot.slot, slot.StackSize);
                suitedSlot = destInventory.GetBestSuitedSlot(slot);
                if (slot.Empty) break;
            }
        }
    }

    private static void SortInventoryInternal(IWorldAccessor world, IInventory inventory)
    {
        // we should probably add checks if the player is allowed to access the inventory

        List<ItemSlot> slots;
        if (inventory is InventoryPlayerBackpacks backpacks)
        {
            slots = backpacks.bagInv.ToList();
        }
        else
        {
            slots = inventory.ToList();
        }

        var itemStacks = slots.Where(s => !s.Empty).Select(s =>
        {
            Debug.Assert(s.Itemstack != null);
            return s.Itemstack.Clone();
        }).ToList();

        // Compact stacks
        var groupedStacks = new List<List<ItemStack>>();
        foreach (var stack in itemStacks)
        {
            if (stack.Collectible == null) continue;
            var i = groupedStacks.FindIndex(list => stack.Equals(world, list.First()));
            if (i != -1)
            {
                groupedStacks[i].Add(stack);
                continue;
            }

            groupedStacks.Add([stack]);
        }

        var compactedStacks = new List<ItemStack>();

        foreach (var group in groupedStacks)
        {
            var maxStackSize = group.First().Collectible.MaxStackSize;
            var totalQuantity = group.Sum(stack => stack.StackSize);
            var firstStack = group.First();

            while (totalQuantity > 0)
            {
                var stackSize = Math.Min(totalQuantity, maxStackSize);
                var newStack = firstStack.Clone();
                newStack.StackSize = stackSize;
                compactedStacks.Add(newStack);
                totalQuantity -= stackSize;
            }
        }

        itemStacks = compactedStacks;


        // Sort by Class, Code, and StackSize
        itemStacks.Sort((a, b) =>
        {
            var classComparison = string.Compare(a.Collectible.Class, b.Collectible.Class, StringComparison.Ordinal);
            if (classComparison != 0) return classComparison;

            var codeComparison = a.Collectible.Code.CompareTo(b.Collectible.Code);
            return codeComparison != 0 ? codeComparison : b.StackSize.CompareTo(a.StackSize);
        });

        // Clear and refill
        for (var i = 0; i < slots.Count; i++)
        {
            slots[i].Itemstack = i < itemStacks.Count ? itemStacks[i] : null;
            slots[i].MarkDirty();
        }
    }

    public override void Dispose()
    {
        _harmony?.UnpatchAll("storagetweaks");
    }
}