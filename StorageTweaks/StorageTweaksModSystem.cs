using System;
using Vintagestory.API.Config;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using ProtoBuf;
using Vintagestory.API.Datastructures;
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

[ProtoContract]
public class UpdateFavoritesPacket
{
    /// <summary>
    /// Collectable Code
    /// </summary>
    [ProtoMember(1)] public required string Code;

    [ProtoMember(2)] public bool IsFavorite;
}

public class StorageTweaksClientConfig
{
    public bool HideFavorites { get; set; }
}

// ReSharper disable once UnusedType.Global
// ReSharper disable once ClassNeverInstantiated.Global
public class StorageTweaksModSystem : ModSystem
{
    private static StorageTweaksClientConfig _config = new();
    private Harmony? _harmony;
    private ICoreServerAPI? _serverApi;
    private ICoreClientAPI? _clientApi;

    /// A list of quality foods and tools to exclude from automatic unloading
    // ReSharper disable once MemberCanBePrivate.Global
    public static readonly List<string> ToolAndFoodCodes = [];

    public override bool ShouldLoad(EnumAppSide forSide) => true;


    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        _harmony = new Harmony("storagetweaks");
        _harmony.PatchAll();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        LoadClientConfig(api);
        api.Network.RegisterChannel("storagetweaks")
            .RegisterMessageType<SortInventoryPacket>()
            .RegisterMessageType<UnloadInventoryPacket>()
            .RegisterMessageType<UpdateFavoritesPacket>();

        FavoritesManager.Initialize(api);
        GuiElementItemSlotGridPatch.SetApi(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _serverApi = api;
        api.Network.RegisterChannel("storagetweaks")
            .RegisterMessageType<SortInventoryPacket>()
            .RegisterMessageType<UnloadInventoryPacket>()
            .RegisterMessageType<UpdateFavoritesPacket>()
            .SetMessageHandler<SortInventoryPacket>(HandleSortInventory)
            .SetMessageHandler<UnloadInventoryPacket>(HandleUnloadInventory)
            .SetMessageHandler<UpdateFavoritesPacket>(HandleUpdateFavorites);

        PopulateToolAndFoodCodes(api);

        api.Event.PlayerJoin += OnPlayerJoin;
    }

    /// <summary>
    /// When a player joins, we check if the "storageTweaksFavorites" attribute is set and if not, set it to a default list.
    /// </summary>
    private static void OnPlayerJoin(IServerPlayer player)
    {
        var tree = player.Entity?.WatchedAttributes;
        if (tree == null) return;

        var favoritesAttr = tree.GetTreeAttribute(FavoritesManager.FavoritesKey);
        if (favoritesAttr != null) return;
        favoritesAttr = new TreeAttribute();
        foreach (var code in ToolAndFoodCodes)
        {
            favoritesAttr.SetBool(code, true);
        }

        tree[FavoritesManager.FavoritesKey] = favoritesAttr;
        tree.MarkPathDirty(FavoritesManager.FavoritesKey);
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

    private static void HandleUpdateFavorites(IServerPlayer fromPlayer, UpdateFavoritesPacket packet)
    {
        var tree = fromPlayer.Entity?.WatchedAttributes;
        if (tree == null) return;

        var favoritesAttr = tree.GetTreeAttribute(FavoritesManager.FavoritesKey);

        if (favoritesAttr == null)
        {
            fromPlayer.Entity?.World.Logger.Error("[StorageTweaks] Favorites attribute not initialized.");
            return;
        }


        if (packet.IsFavorite)
        {
            favoritesAttr.SetBool(packet.Code, packet.IsFavorite);
        }
        else
        {
            favoritesAttr.RemoveAttribute(packet.Code);
        }

        tree.MarkPathDirty(FavoritesManager.FavoritesKey);
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

        // list of item codes that are already in the destination inventory
        var existingCodes = new HashSet<string>();
        foreach (var destSlot in destInventory)
        {
            if (destSlot.Empty) continue;
            if (FavoritesManager.IsFavorite(fromPlayer, destSlot.Itemstack)) continue;
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
        // skip backup slots
        var skipFirstN = sourceInventory is InventoryPlayerBackpacks backpacks ? backpacks.bagSlots.Length : 0;

        List<ItemSlot> ignoredSlots = [];
        foreach (var slot in sourceInventory.Skip(skipFirstN))
        {
            if (slot.Empty) continue;
            if (!existingCodes.Contains(slot.Itemstack.Collectible.Code.ToString())) continue;
            if (slot.GetType().Name != "ItemSlotBagContent") continue;

            ignoredSlots.Clear();
            var world = fromPlayer.Entity.World;
            while (true)
            {
                var op = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge,
                    slot.StackSize);
                var suitedSlot = destInventory.GetBestSuitedSlot(slot, op, ignoredSlots);
                if (suitedSlot.slot == null || suitedSlot.weight == 0) break;
                slot.TryPutInto(suitedSlot.slot, ref op);
                if (slot.Empty) break;
                ignoredSlots.Add(suitedSlot.slot);
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

        // Excludes none vanilla and specialized bag slots from sorting,
        // for example, Quivers And Sheaths item slots
        // Examples: ItemSlotBagContentWithWildcardMatch, ItemSlotTakeOutOnly
        slots = slots.Where(slot => slot.GetType().Name == "ItemSlotBagContent").ToList();

        // Compact stacks
        for (var i = 0; i < slots.Count; i++)
        {
            var sourceSlot = slots[i];
            if (sourceSlot.Empty) continue;

            var stack = sourceSlot.Itemstack;

            // Try to merge this stack into every other suitable slot
            for (var j = 0; j < slots.Count; j++)
            {
                if (i == j) continue; // Don't merge into itself

                var targetSlot = slots[j];
                if (targetSlot.Empty) continue;

                sourceSlot.TryPutInto(world, targetSlot, stack.StackSize);
                if (sourceSlot.Empty) break;
            }
        }

        var itemStacks = slots.Where(s => !s.Empty).Select(x =>
        {
            Debug.Assert(x.Itemstack != null);
            return x.Itemstack.Clone();
        }).ToList();

        // Sort by Class, Code, Contents and StackSize
        itemStacks.Sort((a, b) =>
        {
            var classComparison = string.Compare(a.Collectible.Class, b.Collectible.Class, StringComparison.Ordinal);
            if (classComparison != 0) return classComparison;


            var codeComparison = a.Collectible.Code.CompareTo(b.Collectible.Code);
            if (codeComparison != 0) return codeComparison;

            var contentsA = a.Attributes.GetTreeAttribute("contents")?.ToJsonToken() ?? "";
            var contentsB = b.Attributes.GetTreeAttribute("contents")?.ToJsonToken() ?? "";
            var contentsComparison = string.Compare(contentsA, contentsB, StringComparison.Ordinal);

            return contentsComparison != 0 ? contentsComparison : b.StackSize.CompareTo(a.StackSize);
        });

        // slots are grouped by tags so we can sort mining blocks into mining bag slots, for example
        var slotsGroupedByTags = new Dictionary<EnumItemStorageFlags, List<ItemSlot>>();
        foreach (var slot in slots)
        {
            var group = slotsGroupedByTags.TryGetValue(slot.StorageType);
            if (group != null)
            {
                group.Add(slot);
                continue;
            }

            slotsGroupedByTags.Add(slot.StorageType, [slot]);
        }

        List<(EnumItemStorageFlags, IntRef, List<ItemSlot>)> sortedSlotGroups =
            slotsGroupedByTags.OrderBy(x => BitOperations.PopCount((uint)x.Key))
                .Select(x => (x.Key, IntRef.Create(0), x.Value)).ToList();

        // store sorted items in best slot for item type
        // i.e., mining stuff gets sorted into mining bags and stuff that can't go in specialized bags gets sorted into regular slots
        foreach (var stack in itemStacks)
        {
            var stored = false;
            foreach (var (storageType, i, group) in sortedSlotGroups)
            {
                if (i.GetValue() == group.Count) continue;
                var slot = group[i.GetValue()];
                slot.Itemstack = null;
                // copies some logic from slot.CanHold();
                var canHoldStack =
                    (slot.CanStoreTags.IsEmpty || stack.Collectible.GetTags(stack).Overlaps(slot.CanStoreTags)) &&
                    (stack.Collectible.GetStorageFlags(stack) & storageType) > 0;
                if (!canHoldStack)
                {
                    slot.MarkDirty();
                    continue;
                }

                slot.Itemstack = stack;
                i.SetValue(i.GetValue() + 1);
                stored = true;

                slot.MarkDirty();
                break;
            }

            if (!stored)
            {
                throw new Exception("Failed to store stack while sorting");
            }
        }

        // clear the rest
        foreach (var (_, i1, group) in sortedSlotGroups)
        {
            if (i1.GetValue() >= group.Count) continue;
            for (var i = i1.GetValue(); i < group.Count; i++)
            {
                group[i].Itemstack = null;
                group[i].MarkDirty();
            }
        }
    }

    private static void LoadClientConfig(ICoreAPI api)
    {
        try
        {
            _config = api.LoadModConfig<StorageTweaksClientConfig>("storagetweaks.json");
            if (_config != null) return;
            _config = new StorageTweaksClientConfig();
            api.StoreModConfig(_config, "storagetweaks.json");
        }
        catch (Exception)
        {
            _config = new StorageTweaksClientConfig();
            api.StoreModConfig(_config, "storagetweaks.json");
        }
    }

    public static StorageTweaksClientConfig GetClientConfig()
    {
        return _config;
    }

    public override void Dispose()
    {
        _clientApi?.StoreModConfig(GetClientConfig(), "storagetweaks.json");
        _serverApi?.Event.PlayerJoin -= OnPlayerJoin;
        _harmony?.UnpatchAll("storagetweaks");
    }
}