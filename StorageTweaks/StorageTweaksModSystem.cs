using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HarmonyLib;
using ProtoBuf;
using StorageTweaks.Gui;
using StorageTweaks.Patches;
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

    [ProtoMember(2)] public bool StackPerishables;
}

[ProtoContract]
public class UnloadInventoryPacket
{
    [ProtoMember(1)] public required string InventoryId;

    [ProtoMember(2)] public bool StackPerishables;
}

[ProtoContract]
public class QuickStoreNearbyContainersPacket
{
    [ProtoMember(1)] public bool StackPerishables;
}

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

    /// When true, food with differing perish/spoil progress is stacked on unload,
    /// blending the transition state (same as a manual merge). Default false keeps
    /// the vanilla behavior of not auto-merging differently-perished stacks.
    public bool StackPerishables { get; set; }

    /// When true, the sort & compact button is hidden in inventory and container GUIs.
    /// Sorting via hotkey still works.
    public bool HideSortButton { get; set; }

    /// When true, the quick store nearby button is hidden in the inventory GUI.
    /// Quick store nearby via hotkey still works.
    public bool HideStoreNearbyButton { get; set; }

    /// When true, the force-stack on unload toggle is hidden in the inventory GUI.
    public bool HideStackPerishablesButton { get; set; }

    /// When true, the quick store button is hidden in container GUIs.
    /// Quick store via hotkey still works.
    public bool HideQuickStoreButton { get; set; }
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

        RegisterHotkeys(api);

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
            .SetMessageHandler<SortInventoryPacket>(SortSystem.HandleSortInventory)
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

        UnloadInventory(fromPlayer, destInventory, packet.StackPerishables);
    }

    public static void UnloadInventory(IServerPlayer fromPlayer, IInventory destInventory,
        bool stackPerishables = false)
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

        if (existingCodes.Count == 0)
        {
            Logger().Debug("[StorageTweaks] UnloadInventory: no existing codes in dest ({0}), skipping", destInventory.InventoryID);
            return;
        }

        Logger().Debug("[StorageTweaks] UnloadInventory: dest={0} class={1} slots={2} existingCodes=[{3}] stackPerishables={4}",
            destInventory.InventoryID, destInventory.GetType().Name, destInventory.Count,
            string.Join(",", existingCodes), stackPerishables);

        ProcessInventorySlots(playerInv, destInventory, existingCodes, fromPlayer, stackPerishables);
        ProcessInventorySlots(playerHotbar, destInventory, existingCodes, fromPlayer, stackPerishables);
    }

    [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
    private static void ProcessInventorySlots(IInventory sourceInventory, IInventory destInventory,
        HashSet<string> existingCodes, IServerPlayer fromPlayer, bool stackPerishables)
    {
        List<ItemSlot> ignoredSlots = [];
        foreach (var slot in sourceInventory)
        {
            if (slot.Empty) continue;
            if (!existingCodes.Contains(slot.Itemstack.Collectible.Code.ToString())) continue;
            if (IsExcludedSlot(slot)) continue;

            ignoredSlots.Clear();
            var world = fromPlayer.Entity.World;
            // DirectMerge blends transition state so differently-perished food stacks;
            // AutoMerge (vanilla) refuses to merge stacks with mismatched perish progress.
            var mergePriority = stackPerishables ? EnumMergePriority.DirectMerge : EnumMergePriority.AutoMerge;
            while (true)
            {
                var op = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0, mergePriority,
                    slot.StackSize);
                var suitedSlot = destInventory.GetBestSuitedSlot(slot, op, ignoredSlots);
                if (suitedSlot.slot == null || suitedSlot.weight == 0)
                {
                    // GetBestSuitedSlot hardcodes AutoMerge internally, blocking perishable items
                    // (flour, liquids in FoodShelves) with mismatched transition states. Fall back
                    // to a CanHold-based search to bypass the state gate.
                    // FS slots always use DirectMerge — their GetBestSuitedSlot hardcodes AutoMerge
                    // even when the item is otherwise compatible. Non-FS slots respect mergePriority
                    // so the StackPerishables preference is honoured for regular containers.
                    // Use a fresh ignored set — the normal-path ignoredSlots may contain partially
                    // filled slots that the fallback path can still write into.
                    var fallbackIgnored = new HashSet<ItemSlot>();
                    foreach (var destSlot in destInventory)
                    {
                        if (fallbackIgnored.Contains(destSlot)) continue;
                        if (!destSlot.CanHold(slot))
                        {
                            fallbackIgnored.Add(destSlot);
                            continue;
                        }
                        var isFsSlot = destSlot.GetType().Name.StartsWith("ItemSlotFS");
                        var fallbackPriority = isFsSlot ? EnumMergePriority.DirectMerge : mergePriority;
                        var fallbackOp = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0,
                            fallbackPriority, slot.StackSize);
                        slot.TryPutInto(destSlot, ref fallbackOp);
                        if (fallbackOp.MovedQuantity == 0 && isFsSlot)
                        {
                            // ItemSlotFSUniversal re-checks freshness inside TryPutInto regardless
                            // of merge priority. Merge stacks directly when there is room and the
                            // stacks are content-equal (ignoring transition state).
                            // Collectible-only equality is not enough — empty and filled
                            // bowls/crocks share a Collectible but differ in content attributes.
                            var room = destSlot.MaxSlotStackSize - (destSlot.Itemstack?.StackSize ?? 0);
                            if (room > 0 && !destSlot.Empty &&
                                destSlot.Itemstack!.Equals(world, slot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                            {
                                var toMove = Math.Min(slot.StackSize, room);
                                destSlot.Itemstack.StackSize += toMove;
                                slot.Itemstack.StackSize -= toMove;
                                if (slot.Itemstack.StackSize <= 0) slot.Itemstack = null;
                                destSlot.MarkDirty();
                                slot.MarkDirty();
                            }
                            else
                            {
                                fallbackIgnored.Add(destSlot);
                            }
                        }
                        if (slot.Empty) break;
                    }
                    break;
                }

                slot.TryPutInto(suitedSlot.slot, ref op);
                if (slot.Empty) break;
                ignoredSlots.Add(suitedSlot.slot);
            }
        }
    }

    public static bool IsExcludedSlot(ItemSlot slot)
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

    private static void RegisterHotkeys(ICoreClientAPI api)
    {
        api.Input.RegisterHotKey("storagetweaks.sort",
            Lang.Get("storagetweaks:hotkey-sort-inventory"),
            GlKeys.A, HotkeyType.InventoryHotkeys, true, true, true);

        api.Input.RegisterHotKey("storagetweaks.sortcontainer",
            Lang.Get("storagetweaks:hotkey-sort-container"),
            GlKeys.B, HotkeyType.InventoryHotkeys, true, true, true);

        api.Input.RegisterHotKey("storagetweaks.storenearby",
            Lang.Get("storagetweaks:hotkey-store-nearby"),
            GlKeys.C, HotkeyType.InventoryHotkeys, true, true, true);

        api.Input.SetHotKeyHandler("storagetweaks.sort", _ =>
        {
            var inv = api.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
            if (inv == null) return false;

            PatchUtils.SendPacket(api, new SortInventoryPacket
            {
                InventoryId = inv.InventoryID,
                StackPerishables = GetClientConfig().StackPerishables
            });
            return true;
        });

        api.Input.SetHotKeyHandler("storagetweaks.sortcontainer", _ =>
        {
            var stackPerishables = GetClientConfig().StackPerishables;
            var count = 0;
            foreach (var dialog in api.Gui.OpenedGuis)
            {
                var composer = dialog.SingleComposer;
                if (composer?.DialogName == null) continue;
                if (!GuiDialogBlockEntityInventoryPatch.DialogNamePrefixes.Any(prefix =>
                        composer.DialogName.StartsWith(prefix, StringComparison.Ordinal))) continue;

                var inv = PatchUtils.GetInventoryForComposer(composer);

                if (inv == null) continue;

                PatchUtils.SendPacket(api, new SortInventoryPacket
                {
                    InventoryId = inv.InventoryID,
                    StackPerishables = stackPerishables
                });
                count += 1;
            }

            return count > 0;
        });

        api.Input.SetHotKeyHandler("storagetweaks.storenearby", _ =>
        {
            PatchUtils.SendPacket(api, new QuickStoreNearbyContainersPacket
            {
                StackPerishables = GetClientConfig().StackPerishables
            });
            return true;
        });
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll("storagetweaks");
        capi?.StoreModConfig(GetClientConfig(), "storagetweaks.json");
        if (sapi != null) sapi.Event.PlayerJoin -= OnPlayerJoin;
    }
}