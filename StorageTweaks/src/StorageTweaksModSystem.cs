using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using ConfigLib;
using HarmonyLib;
using ProtoBuf;
using StorageTweaks.Extensions;
using StorageTweaks.Gui;
using StorageTweaks.Patches;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace StorageTweaks;

[ProtoContract]
public class SortInventoryPacket
{
    [ProtoMember(1)] public required string InventoryId;

    [ProtoMember(4)] public bool SkipFavoritesWhenSorting;

    [ProtoMember(3)] public bool SortHotbarWithBackpack = false;

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
public class RemeshContainersPacket
{
    /// Container block positions whose baked display mesh needs to be rebuilt on
    /// the client after a server-side inventory mutation (e.g. quick store nearby).
    /// Targets Food Shelves BlockEntityDisplay subclasses that cache their content
    /// mesh in `blockMesh` and only refresh it from their own `InitMesh()` (e.g.
    /// the Ceiling Rack), other container kinds are filtered out server-side.
    [ProtoMember(1)] public List<BlockPos> Positions = [];
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

    /// When true, the sort button in the backpack (player inventory) will also sort
    /// items in the hotbar alongside the backpack slots
    public bool SortHotbarWithBackpack { get; set; }

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

    /// When true, favorited items are excluded from sorting (they won't be moved or merged).
    public bool SkipFavoritesWhenSorting { get; set; }
}

public class StorageTweaksServerConfig
{
    /// The search radius (in blocks) used when quick storing nearby containers.
    /// The search area is a cube of (2*radius+1)^3 blocks centered on the player.
    public int QuickStoreNearbySearchRadius { get; set; } = 8;

    /// Additional container patterns merged with the built-in whitelist for quick storing nearby.
    public List<string> AdditionalContainerWhitelist { get; set; } = [];

    /// Container patterns that are never considered by quick store nearby, even when matched by the
    /// built-in or additional whitelist.
    public List<string> ContainerBlacklist { get; set; } = [];
}

// ReSharper disable once UnusedType.Global
// ReSharper disable once ClassNeverInstantiated.Global
public class StorageTweaksModSystem : ModSystem
{
    private static StorageTweaksClientConfig config = new();
    private static StorageTweaksServerConfig serverConfig = new();

    /// A list of quality foods and tools to exclude from automatic unloading
    // ReSharper disable once MemberCanBePrivate.Global
    public static readonly List<string> ToolAndFoodCodes = [];

    private static ILogger? logger;
    public ContainerActionButtons? ContainerActionButtons;
    public FavoritesManager? FavoritesManager;
    public InventoryActionButtons? InventoryActionButtons;

    private ICoreClientAPI? capi;
    private Harmony? harmony;
    private ICoreServerAPI? sapi;

    // ReSharper disable once MemberCanBePrivate.Global
    public static ILogger Logger()
    {
        return logger!;
    }

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
            .RegisterMessageType<QuickStoreNearbyContainersPacket>()
            .RegisterMessageType<RemeshContainersPacket>()
            .SetMessageHandler<RemeshContainersPacket>(HandleRemeshContainersPacket);
        capi.Logger.VerboseDebug("[StorageTweaks] Registered channels client side");

        FavoritesManager = new FavoritesManager(capi);
        capi.Logger.VerboseDebug("[StorageTweaks] Initialized favorites manager client side");
        InventoryActionButtons = new InventoryActionButtons(capi);
        capi.Logger.VerboseDebug("[StorageTweaks] Initialized inventory action buttons");
        ContainerActionButtons = new ContainerActionButtons(capi);
        capi.Logger.VerboseDebug("[StorageTweaks] Initialized container action buttons");
        harmony = new Harmony("storagetweaks");
        harmony.PatchAll();
        MoreInventorysPatch.Apply(harmony, capi);
        capi.Logger.VerboseDebug("[StorageTweaks] Completed harmony patches");

        RegisterHotkeys(api);

        if (api.ModLoader.IsModEnabled("configlib"))
        {
            SubscribeToConfigChange(api);
        }

        capi.Logger.VerboseDebug("[StorageTweaks] Started StorageTweaksModSystem client side");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        sapi.Logger.VerboseDebug("[StorageTweaks] Starting StorageTweaksModSystem server side");
        LoadServerConfig(api);
        api.Network.RegisterChannel("storagetweaks")
            .RegisterMessageType<SortInventoryPacket>()
            .RegisterMessageType<UnloadInventoryPacket>()
            .RegisterMessageType<UpdateFavoritesPacket>()
            .RegisterMessageType<QuickStoreNearbyContainersPacket>()
            .RegisterMessageType<RemeshContainersPacket>()
            .SetMessageHandler<SortInventoryPacket>(SortSystem.HandleSortInventory)
            .SetMessageHandler<UnloadInventoryPacket>(HandleUnloadInventory)
            .SetMessageHandler<UpdateFavoritesPacket>(HandleUpdateFavorites)
            .SetMessageHandler<QuickStoreNearbyContainersPacket>(QuickStoreNearbyContainerSystem
                .HandleQuickStoreNearbyContainers);
        sapi.Logger.VerboseDebug("[StorageTweaks] Registered channels server side");

        PopulateToolAndFoodCodes(api);
        sapi.Logger.VerboseDebug("[StorageTweaks] Populated tool and food codes");

        ContainerWhitelist.InitWhitelist(api);

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
        if (tree == null)
        {
            return;
        }

        var favoritesAttr = tree.GetTreeAttribute(FavoritesManager.FavoritesKey);
        if (favoritesAttr != null)
        {
            return;
        }

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
            "torch",
        };
        var excludeKeywords = new[]
        {
            "blade", "part", "raw", "stackrandomizer", "toolmold", "-down", "-north", "-east", "-south", "-west",
        };

        foreach (var collectible in api.World.Items.Concat(api.World.Collectibles))
        {
            if (collectible.Code == null)
            {
                continue;
            }

            var code = collectible.Code.ToString();
            var parts = code.Split(':', '-');

            // Check if any part matches a keyword
            if (!keywords.Any(k => parts.Any(p => string.Equals(p, k, StringComparison.OrdinalIgnoreCase))))
            {
                continue;
            }

            // Exclude unwanted items
            if (excludeKeywords.Any(k => code.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }


            ToolAndFoodCodes.Add(code);
        }

        api.World.Logger.Debug("[StorageTweaks] Populated {0} tool and food codes.", ToolAndFoodCodes.Count);
    }

    private static void HandleUpdateFavorites(IServerPlayer fromPlayer, UpdateFavoritesPacket packet)
    {
        var tree = fromPlayer.Entity?.WatchedAttributes;
        if (tree == null)
        {
            return;
        }

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
            if (destSlot.Empty)
            {
                continue;
            }

            if (destSlot.Itemstack == null)
            {
                continue;
            }

            // try catching here because one user got a null reference exception
            // no idea how because destSlot.Empty above should ensure that Itemstack is not null
            // https://mods.vintagestory.at/storagetweaks#cmt-193057
            try
            {
                if (FavoritesManager.IsFavorite(fromPlayer, destSlot.Itemstack))
                {
                    continue;
                }
            }
            catch (Exception e)
            {
                Logger().Error("[StorageTweaks] IsFavorite threw exception with item stack: {0}, {1}", destSlot.Itemstack, destSlot.Itemstack?.Collectible);
                Logger().Error("[StorageTweaks] HandleUnloadInventory: Exception {0}", e);
                continue;
            }

            existingCodes.Add(destSlot.Itemstack.Collectible.Code.ToString());
        }

        if (existingCodes.Count == 0)
        {
            Logger().VerboseDebug("[StorageTweaks] UnloadInventory: no existing codes in dest ({0}), skipping", destInventory.InventoryID);
            return;
        }

        Logger().VerboseDebug("[StorageTweaks] UnloadInventory: dest={0} class={1} slots={2} existingCodes=[{3}] stackPerishables={4}",
            destInventory.InventoryID, destInventory.GetType().Name, destInventory.Count,
            string.Join(",", existingCodes), stackPerishables);

        // Food Shelves containers cap items per *segment* (a run of ItemsPerSegment slots)
        // based on item size - the vanilla GetBestSuitedSlot path we use ignores that cap,
        // so we wrap the destination inventory in a segment guard that pre-blocks slots in
        // already-full segments. The guard is disabled when Food Shelves isn't loaded.
        var segmentGuard = FoodShelvesSegmentGuard.For(destInventory, fromPlayer.Entity.World);

        ProcessInventorySlots(playerInv, destInventory, existingCodes, fromPlayer, stackPerishables,
            segmentGuard);
        ProcessInventorySlots(playerHotbar, destInventory, existingCodes, fromPlayer, stackPerishables,
            segmentGuard);
    }

    [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
    private static void ProcessInventorySlots(IInventory sourceInventory, IInventory destInventory,
        HashSet<string> existingCodes, IServerPlayer fromPlayer, bool stackPerishables,
        FoodShelvesSegmentGuard segmentGuard)
    {
        List<ItemSlot> ignoredSlots = [];
        foreach (var slot in sourceInventory)
        {
            if (slot.Empty)
            {
                continue;
            }

            if (!existingCodes.Contains(slot.Itemstack!.Collectible.Code.ToString()))
            {
                continue;
            }

            if (!slot.CanSortInto())
            {
                continue;
            }

            ignoredSlots.Clear();
            // Seed ignored slots with those whose segment is already at the per-segment cap
            // for this source stack (Food Shelves itemsPerSegment layout). For non-Food Shelves
            // inventories (or bulk slots) the guard returns nothing, so this is a no-op.
            if (segmentGuard.Enabled)
            {
                ignoredSlots.AddRange(segmentGuard.FullSlotsFor(destInventory, slot.Itemstack!));
            }

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
                    var fallbackIgnored = new HashSet<ItemSlot>();
                    foreach (var destSlot in destInventory)
                    {
                        if (fallbackIgnored.Contains(destSlot))
                        {
                            continue;
                        }

                        // Honor the Food Shelves per-segment cap: a slot whose segment is at capacity
                        // is treated as "cannot hold" so we don't overstuff that segment.
                        if (segmentGuard.Enabled && segmentGuard.SegmentIsFull(destSlot, slot.Itemstack!))
                        {
                            fallbackIgnored.Add(destSlot);
                            continue;
                        }

                        if (!destSlot.CanHold(slot))
                        {
                            fallbackIgnored.Add(destSlot);
                            continue;
                        }

                        var isFsSlot = destSlot.GetType().Name.StartsWith("ItemSlotFS"); //FoodStorage stack merge override
                        var fallbackPriority = isFsSlot ? EnumMergePriority.DirectMerge : mergePriority;
                        var fallbackOp = new ItemStackMoveOperation(world, EnumMouseButton.Left, 0,
                            fallbackPriority, slot.StackSize);
                        slot.TryPutInto(destSlot, ref fallbackOp);
                        if (fallbackOp.MovedQuantity == 0 && isFsSlot)
                        {
                            var room = destSlot.MaxSlotStackSize - (destSlot.Itemstack?.StackSize ?? 0);
                            if (room > 0 && !destSlot.Empty &&
                                destSlot.Itemstack!.Equals(world, slot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                            {
                                var toMove = Math.Min(slot.StackSize, room);
                                destSlot.Itemstack.StackSize += toMove;
                                slot.Itemstack!.StackSize -= toMove;
                                if (slot.Itemstack.StackSize <= 0)
                                {
                                    slot.Itemstack = null;
                                }

                                destSlot.MarkDirty();
                                slot.MarkDirty();
                            }
                            else
                            {
                                fallbackIgnored.Add(destSlot);
                            }
                        }

                        if (slot.Empty)
                        {
                            break;
                        }
                    }

                    break;
                }

                slot.TryPutInto(suitedSlot.slot, ref op);
                // After a successful TryPutInto a Food Shelves segment may now be at its cap
                // for this stack. Block its remaining slots so the next GetBestSuitedSlot
                // iteration skips them (mirrors Food Shelves' own TryPut exit condition).
                if (segmentGuard.Enabled && !slot.Empty)
                {
                    if (segmentGuard.SegmentIsFull(suitedSlot.slot, slot.Itemstack!))
                    {
                        ignoredSlots.AddRange(BlockSegmentSiblings(destInventory, segmentGuard, slot.Itemstack));
                    }
                }

                if (slot.Empty)
                {
                    break;
                }

                ignoredSlots.Add(suitedSlot.slot);
            }
        }
    }

    /// <summary>
    ///     Re-scans the destination inventory for Food Shelves segments that just reached
    ///     their GetSegmentLimit cap as a result of a recent TryPutInto, and returns the remaining
    ///     slots for each segment that should be skipped.
    /// </summary>
    private static HashSet<ItemSlot> BlockSegmentSiblings(IInventory destInventory,
        FoodShelvesSegmentGuard segmentGuard, ItemStack sourceStack)
    {
        return !segmentGuard.Enabled ? [] : segmentGuard.FullSlotsFor(destInventory, sourceStack);
    }

    private static void LoadClientConfig(ICoreAPI api)
    {
        try
        {
            config = api.LoadModConfig<StorageTweaksClientConfig>("storagetweaks.json");
            if (config != null)
            {
                return;
            }

            config = new StorageTweaksClientConfig();
            api.StoreModConfig(config, "storagetweaks.json");
        }
        catch (Exception)
        {
            config = new StorageTweaksClientConfig();
            api.StoreModConfig(config, "storagetweaks.json");
        }
    }

    private static void LoadServerConfig(ICoreServerAPI api)
    {
        try
        {
            serverConfig = api.LoadModConfig<StorageTweaksServerConfig>("storagetweaks-server.json");
            if (serverConfig != null)
            {
                return;
            }

            serverConfig = new StorageTweaksServerConfig();
            api.StoreModConfig(serverConfig, "storagetweaks-server.json");
        }
        catch (Exception)
        {
            serverConfig = new StorageTweaksServerConfig();
            api.StoreModConfig(serverConfig, "storagetweaks-server.json");
        }
    }

    public static StorageTweaksClientConfig GetClientConfig()
    {
        return config;
    }

    public static StorageTweaksServerConfig GetServerConfig()
    {
        return serverConfig;
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
            if (inv == null)
            {
                return false;
            }

            PatchUtils.SendPacket(api, new SortInventoryPacket
            {
                InventoryId = inv.InventoryID,
                StackPerishables = GetClientConfig().StackPerishables,
                SkipFavoritesWhenSorting = GetClientConfig().SkipFavoritesWhenSorting,
            });
            return true;
        });

        api.Input.SetHotKeyHandler("storagetweaks.sortcontainer", _ =>
        {
            var stackPerishables = GetClientConfig().StackPerishables;
            var ignoreFavorites = GetClientConfig().SkipFavoritesWhenSorting;
            var count = 0;
            foreach (var dialog in api.Gui.OpenedGuis)
            {
                var composer = dialog.SingleComposer;
                if (composer?.DialogName == null)
                {
                    continue;
                }

                if (!GuiDialogBlockEntityInventoryPatch.DialogNamePrefixes.Any(prefix =>
                        composer.DialogName.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    continue;
                }

                var inv = PatchUtils.GetInventoryForComposer(composer);

                if (inv == null)
                {
                    continue;
                }

                PatchUtils.SendPacket(api, new SortInventoryPacket
                {
                    InventoryId = inv.InventoryID,
                    StackPerishables = stackPerishables,
                    SkipFavoritesWhenSorting = ignoreFavorites,
                });
                count += 1;
            }

            return count > 0;
        });

        api.Input.SetHotKeyHandler("storagetweaks.storenearby", _ =>
        {
            PatchUtils.SendPacket(api, new QuickStoreNearbyContainersPacket
            {
                StackPerishables = GetClientConfig().StackPerishables,
            });
            return true;
        });
    }

    private static void SubscribeToConfigChange(ICoreClientAPI api)
    {
        var system = api.ModLoader.GetModSystem<ConfigLibModSystem>();

        system.SettingChanged += (domain, _, setting) =>
        {
            if (domain != "storagetweaks")
            {
                return;
            }

            setting.AssignSettingValue(config);
            GuiDialogInventoryPatch.Reload(api);
        };
        system.ConfigsLoaded += () =>
        {
            system.GetConfig("storagetweaks")?.AssignSettingsValues(config);
        };
    }

    /// <summary>
    ///     Client-side handler for <see cref="RemeshContainersPacket" />. Tells Food Shelves
    ///     display containers (and similar mods) to rebuild their baked-in content mesh after
    ///     a server-side inventory mutation (e.g. quick store nearby). Such containers cache
    ///     their content mesh in <c>blockMesh</c> via their own <c>InitMesh()</c>, which the
    ///     regular <c>BlockEntityDisplay.MarkMeshesDirty()</c> (triggered by <c>MarkDirty(true)</c>)
    ///     does not refresh. Deferred by one tick so the concurrent BE tree-attribute sync
    ///     (also queued by <c>MarkDirty(true)</c>) has landed and the client-side inventory
    ///     <c>InitMesh</c> reads from is current.
    /// </summary>
    private void HandleRemeshContainersPacket(RemeshContainersPacket packet)
    {
        if (capi == null || packet.Positions.Count == 0)
        {
            return;
        }

        capi.Event.RegisterCallback(_ =>
        {
            foreach (var pos in packet.Positions)
            {
                var be = capi.World.BlockAccessor.GetBlockEntity(pos);
                if (be == null)
                {
                    continue;
                }

                var initMesh = FindInitMesh(be.GetType());
                if (initMesh == null)
                {
                    continue;
                }

                try
                {
                    initMesh.Invoke(be, null);
                }
                catch (Exception e)
                {
                    capi.Logger.Warning("[StorageTweaks] InitMesh invoke failed on {0} at {1}: {2}",
                        be.GetType().Name, pos, e);
                    continue;
                }

                // Re-tessellate the chunk so OnTesselation picks up the refreshed blockMesh.
                capi.World.BlockAccessor.MarkBlockDirty(pos);
            }
        }, 100);
    }

    /// <summary>
    ///     Walks the type hierarchy looking for a parameterless <c>InitMesh()</c> instance method
    ///     (the Food Shelves pattern; declared as protected). <see cref="BindingFlags.NonPublic" />
    ///     does not return inherited members, so we walk up the chain ourselves. Returns null
    ///     when the BE type has no InitMesh (vanilla chests, baskets, etc.) so those are skipped.
    /// </summary>
    private static MethodInfo? FindInitMesh(Type? type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                                   BindingFlags.DeclaredOnly;
        while (type != null && type != typeof(object))
        {
            var method = type.GetMethod("InitMesh", flags);
            if (method != null && method.GetParameters().Length == 0)
            {
                return method;
            }

            type = type.BaseType;
        }

        return null;
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll("storagetweaks");
        capi?.StoreModConfig(GetClientConfig(), "storagetweaks.json");
        sapi?.StoreModConfig(GetServerConfig(), "storagetweaks-server.json");
        if (sapi != null)
        {
            sapi.Event.PlayerJoin -= OnPlayerJoin;
        }
    }
}
