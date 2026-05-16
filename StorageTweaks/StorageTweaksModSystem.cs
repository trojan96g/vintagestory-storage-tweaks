using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using ProtoBuf;
using StorageTweaks.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

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
public class QuickStoreNearbyContainersPacket;

[ProtoContract]
public class UpdateFavoritesPacket
{
    /// <summary>
    ///     Collectable Code
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
    public InventoryActionButtons? InventoryActionButtons;
    public ContainerActionButtons? ContainerActionButtons;
    public FavoritesManager? FavoritesManager;

    private static readonly string[] SlotTypes =
    [
        "ItemSlotSurvival",
        "ItemSlotBagContent",
        // for overhaullib before 1.22, Quivers And Sheaths and Backpacks mod use this slot type before 1.22
        "ItemSlotBagContentWithWildcardMatch",
        // for https://mods.vintagestory.at/playerinventorylib used by backpacks mod in 1.22+
        "BackpackSlot",
        // for https://mods.vintagestory.at/playerinventorylib without the Backpacks mod
        "VanillaBagContentSlot"
    ];

    private static StorageTweaksClientConfig config = new();

    /// A list of quality foods and tools to exclude from automatic unloading
    // ReSharper disable once MemberCanBePrivate.Global
    public static readonly List<string> ToolAndFoodCodes = [];

    private ICoreClientAPI? capi;
    private Harmony? harmony;
    private ICoreServerAPI? sapi;
    private static ILogger? logger;

    // ReSharper disable once MemberCanBePrivate.Global
    public static ILogger Logger() => logger!;

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return true;
    }

    public override void Start(ICoreAPI api)
    {
        api.Logger.VerboseDebug("[StorageTweaks] Starting StorageTweaksModSystem {0}", api.GetType().Name);
        logger = api.Logger;
    }

    public override void StartPre(ICoreAPI api)
    {
        api.Logger.VerboseDebug("[StorageTweaks] PreStart StorageTweaksModSystem {0}", api.GetType().Name);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        capi.Logger.VerboseDebug("[StorageTweaks] Starting StorageTweaksModSystem client side");
        LoadClientConfig(api);
        capi.Logger.VerboseDebug("Loaded client config");
        api.Network.RegisterChannel("storagetweaks")
            .RegisterMessageType<SortInventoryPacket>()
            .RegisterMessageType<UnloadInventoryPacket>()
            .RegisterMessageType<UpdateFavoritesPacket>()
            .RegisterMessageType<QuickStoreNearbyContainersPacket>();
        capi.Logger.VerboseDebug("[StorageTweaks] Registered channels client side");

        FavoritesManager = new FavoritesManager(capi);
        capi.Logger.VerboseDebug("[StorageTweaks] Initialized favorites manager client side");
        InventoryActionButtons = new InventoryActionButtons(capi);
        capi.Logger.VerboseDebug("[StorageTweaks] Initialized inventory action buttons");
        ContainerActionButtons = new ContainerActionButtons(capi);
        capi.Logger.VerboseDebug("[StorageTweaks] Initialized container action buttons");
        harmony = new Harmony("storagetweaks");
        harmony.PatchAll();
        capi.Logger.VerboseDebug("[StorageTweaks] Completed harmony patches");

        capi.Logger.VerboseDebug("[StorageTweaks] Started StorageTweaksModSystem client side");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        sapi.Logger.VerboseDebug("[StorageTweaks] Starting StorageTweaksModSystem server side");
        api.Network.RegisterChannel("storagetweaks")
            .RegisterMessageType<SortInventoryPacket>()
            .RegisterMessageType<UnloadInventoryPacket>()
            .RegisterMessageType<UpdateFavoritesPacket>()
            .RegisterMessageType<QuickStoreNearbyContainersPacket>()
            .SetMessageHandler<SortInventoryPacket>(HandleSortInventory)
            .SetMessageHandler<UnloadInventoryPacket>(HandleUnloadInventory)
            .SetMessageHandler<UpdateFavoritesPacket>(HandleUpdateFavorites)
            .SetMessageHandler<QuickStoreNearbyContainersPacket>(QuickStoreNearbyContainerSystem
                .HandleQuickStoreNearbyContainers);
        sapi.Logger.VerboseDebug("[StorageTweaks] Registered channels server side");

        PopulateToolAndFoodCodes(api);
        sapi.Logger.VerboseDebug("[StorageTweaks] Populated tool and food codes");

        api.Event.PlayerJoin += OnPlayerJoin;

        sapi.Logger.VerboseDebug("[StorageTweaks] Starting StorageTweaksModSystem server side");
    }

    /// <summary>
    ///     When a player joins, we check if the "storageTweaksFavorites" attribute is set and if not, set it to a default
    ///     list.
    /// </summary>
    private static void OnPlayerJoin(IServerPlayer player)
    {
        var tree = player.Entity?.WatchedAttributes;
        if (tree == null) return;

        var favoritesAttr = tree.GetTreeAttribute(FavoritesManager.FavoritesKey);
        if (favoritesAttr != null) return;

        favoritesAttr = new TreeAttribute();
        foreach (var code in ToolAndFoodCodes) favoritesAttr.SetBool(code, true);

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


        if (packet.IsFavorite) favoritesAttr.SetBool(packet.Code, packet.IsFavorite);
        else favoritesAttr.RemoveAttribute(packet.Code);

        tree.MarkPathDirty(FavoritesManager.FavoritesKey);
    }

    private static void HandleUnloadInventory(IServerPlayer fromPlayer, UnloadInventoryPacket packet)
    {
        // should probably add checks if the player is allowed to access the inventory

        var destInventory = fromPlayer.InventoryManager.GetInventory(packet.InventoryId);
        if (destInventory == null)
        {
            Logger().Debug(
                "[StorageTweaks] HandleUnloadInventory: Destination inventory not found");
            return;
        }

        UnloadInventory(fromPlayer, destInventory);
    }

    public static void UnloadInventory(IServerPlayer fromPlayer, IInventory destInventory)
    {
        var playerInv = fromPlayer.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (playerInv == null)
        {
            Logger().Debug(
                "[StorageTweaks] HandleUnloadInventory: Player backpack inventory not found");
            return;
        }

        var playerHotbar = fromPlayer.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
        if (playerHotbar == null)
        {
            Logger().Debug(
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

        if (existingCodes.Count == 0) return;

        ProcessInventorySlots(playerInv, destInventory, existingCodes, fromPlayer);
        ProcessInventorySlots(playerHotbar, destInventory, existingCodes, fromPlayer);
    }

    private static void ProcessInventorySlots(IInventory sourceInventory, IInventory destInventory,
        HashSet<string> existingCodes, IServerPlayer fromPlayer)
    {
        List<ItemSlot> ignoredSlots = [];
        foreach (var slot in sourceInventory)
        {
            if (slot.Empty) continue;
            if (!existingCodes.Contains(slot.Itemstack.Collectible.Code.ToString())) continue;
            if (IsExcludedSlot(slot)) continue;

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

        var slots = inventory.ToList();

        // Excludes specialized bag slots from sorting,
        // for example, Quivers And Sheaths item slots
        // Examples: ItemSlotBagContentWithWildcardMatch, ItemSlotTakeOutOnly
        slots = slots.Where(slot => !IsExcludedSlot(slot)).ToList();

        // Clone inventory into DummySlots so all changes are made on the clone
        var clonedSlots = slots.Select(s => new DummySlot(s.Itemstack?.Clone())).ToList();

        // Compact stacks
        for (var i = 0; i < clonedSlots.Count; i++)
        {
            var sourceSlot = clonedSlots[i];
            if (sourceSlot.Empty) continue;

            var stack = sourceSlot.Itemstack;

            // Try to merge this stack into every other suitable slot
            for (var j = 0; j < clonedSlots.Count; j++)
            {
                if (i == j) continue; // Don't merge into itself

                var targetSlot = clonedSlots[j];
                if (targetSlot.Empty) continue;

                sourceSlot.TryPutInto(world, targetSlot, stack.StackSize);
                if (sourceSlot.Empty) break;
            }
        }

        var itemStacks = clonedSlots.Where(s => !s.Empty).Select(x =>
        {
            Debug.Assert(x.Itemstack != null);
            return x.TakeOutWhole();
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

        // Fill cloned slots sequentially with sorted stacks
        var fillIndex = 0;
        foreach (var stack in itemStacks)
            clonedSlots[fillIndex++].Itemstack = stack;

        // Hotswap: apply final cloned state to actual inventory slots
        for (var i = 0; i < slots.Count; i++)
        {
            slots[i].Itemstack = clonedSlots[i].Itemstack;
            slots[i].MarkDirty();
        }
    }

    private static bool IsExcludedSlot(ItemSlot slot)
    {
        return !SlotTypes.Contains(slot.GetType().Name);
    }

    private static void LoadClientConfig(ICoreAPI api)
    {
        try
        {
            config = api.LoadModConfig<StorageTweaksClientConfig>("storagetweaks.json");
            if (config != null) return;

            config = new StorageTweaksClientConfig();
            api.StoreModConfig(config, "storagetweaks.json");
        }
        catch (Exception)
        {
            config = new StorageTweaksClientConfig();
            api.StoreModConfig(config, "storagetweaks.json");
        }
    }

    public static StorageTweaksClientConfig GetClientConfig()
    {
        return config;
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll("storagetweaks");
        capi?.StoreModConfig(GetClientConfig(), "storagetweaks.json");
        if (sapi != null) sapi.Event.PlayerJoin -= OnPlayerJoin;
    }
}