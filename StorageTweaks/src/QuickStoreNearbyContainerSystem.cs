using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace StorageTweaks;

public static class QuickStoreNearbyContainerSystem
{
    // Chest types to include when looking for nearby chests to quick deposit matching items from inventory
    private static readonly HashSet<(string, string)> QuickStackChestTypes =
    [
        // vanilla baskets
        ("basket", "reed"),
        ("basket", "papyrus"),
        ("basket", "aged"),

        // vanilla chests
        ("chest", "normal-labeled"),
        ("chest", "normal-generic"),
        ("chest", "normal"),
        ("chest", "normal-aged"),

        // vanilla crates
        ("crate", "crate"),

        // String Sense mod containers
        ("basket", "bark"),
        ("basket", "vine"),
        ("basket", "mixed"),
        ("basket", "flax"),
        ("basket", "straw"),

        // Better Crates mod containers
        ("bettercrate", "bettercrate2sided"),
        ("bettercrate", "bettercrate"),

        // Extra Chests mod and Upgradeable Storage mod
        ("chest", "blackbronze"),
        ("chest", "iron"),
        ("chest", "bismuthbronze"),
        ("chest", "steel"),
        ("chest", "tinbronze"),
        ("chest", "copper"),

        // Containers Bundle mod
        ("chest", "strongbox"),
        ("chest", "metalcabinetnolabel"),
        ("chest", "bamboochest"),
        ("chest", "cupboardnolabel"),
        ("chest", "stonecasket"),
        ("chest", "cupboardwithlabel"),
        ("chest", "woodenbox"),
        ("chest", "exquisitechest"),
        ("chest", "wickerbasket"),
        ("chest", "foodcupboard"),
        ("chest", "longcrate"),
        ("chest", "linencrate"),
        ("chest", "foodcupboardwall"),

        // Purposeful Storage mod
        ("pantsrack", "pantsrack"),
        ("necklacestand", "necklacestand"),
        ("shoerack", "shoerack"),
        ("hatrack", "hatrack"),
        ("wardrobe", "wardrobe"),
        ("swordpedestal", "swordpedestal"),
        ("gloverack", "gloverack"),
        ("blanketrack", "blanketrack"),
        ("weaponrack", "weaponrack"),
        ("belthooks", "belthooks"),
        ("butterflydisplaypanel", "butterflydisplaypanel"),
        ("swordplaque", "swordplaque"),
        ("gearrack", "gearrack"),
        ("medallionrack", "medallionrack"),
        ("saddlerack", "saddlerack"),
        ("schematicrack", "schematicrack"),
        ("tuningcylinderrack", "tuningcylinderrack"),
        ("resourcebin", "resourcebin"),
        ("spearrack", "spearrack"),
        ("glidermount", "glidermount"),

        // Food Shelves mod - shelves & display
        ("doubleshelf", "doubleshelf"),
        ("breadshelf", "breadshelf"),
        ("barshelf", "barshelf"),
        ("eggshelf", "eggshelf"),
        ("pieshelf", "pieshelf"),
        ("seedshelf", "seedshelf"),
        ("sushishelf", "sushishelf"),
        ("tablewshelf", "tablewshelf"),
        ("fooddisplaycase", "fooddisplaycase"),
        ("fooddisplayblock", "fooddisplayblock"),
        ("pumpkincase", "pumpkincase"),

        // Food Shelves mod - specialty storage
        ("floursack", "floursack"),
        ("jar", "jar"),
        ("jarlarge", "jarlarge"),
        ("jarstand", "jarstand"),
        ("ceilingrack", "ceilingrack"),
        ("seedbins", "seedbins"),
        ("buckethook", "buckethook"),

        // Food Shelves mod - coolers
        ("coolingcabinet", "coolingcabinet"),
        ("meatfreezer", "meatfreezer"),
        ("fruitcooler", "fruitcooler"),
        ("wallcabinet", "wallcabinet"),

        // Food Shelves mod - baskets
        ("fruitbasket", "fruitbasket"),
        ("vegetablebasket", "vegetablebasket"),
        ("eggbasket", "eggbasket"),
        ("mushroombasket", "mushroombasket"),

        // Food Shelves mod - barrel/tun racks
        ("barrelrack", "barrelrack"),
        ("tunrack", "tunrack"),

        // Upgradeable Storage - Labeled Storage Vessels (all color variants)
        ("chest", "generic"),

        // MoreInventories
        ("firstshelfinventory", "firstshelf"), // Food Shelf v.1
        ("mibasketclosed", "mibasketclosed"), // Cattail Basket
        ("micrateclosed", "micrateclosed"), // Closed Crate
        ("rackhorizontal2x2dynamic", "rackhorizontal2x2"), // Double Rack 2x2
        ("rackhorizontal2x2dynamic", "rackhorizontalframecorner2x2"), // Double Rack Frame Corner 2x2
        ("rackhorizontal2x2dynamic", "rackhorizontalframeline2x2"),
        ("rackhorizontaldynamic", "rackhorizontal"), // Double Rack 2x3
        ("rackhorizontaldynamic", "rackhorizontalframecorner"), // Double Rack Frame Corner 2x3
        ("rackhorizontaldynamic", "rackhorizontalframeline"),
        ("rackhorizontalwood2x2dynamic", "rackhorizontalwood2x2"),
        ("rackhorizontalwood2x3dynamic", "rackhorizontalwood2x3"),
        ("rackstick1x2dynamic", "rackstick1x2"),
        ("rackstickdynamic", "rackstick"),
        ("rackvertical1x2onedynamic", "rackvertical1x2"),
        ("rackvertical1x2onedynamic", "rackverticalframecorner1x2"),
        ("rackvertical1x2onedynamic", "rackverticalframeline1x2"),
        ("rackverticalonedynamic", "rackvertical"),
        ("rackverticalonedynamic", "rackverticalframecorner"),
        ("rackverticalonedynamic", "rackverticalframeline"),
        ("shieldstand", "shieldstand"),
        ("smallHorizontleWeaponStandInventory", "smallhorizontleswordstand"),
        ("smallVerticalWeaponStandInventory", "smallverticalweaponstand"),
    ];

    private static List<BlockEntityContainer> GetNearbyContainers(IWorldAccessor world, BlockPos position,
        int radius)
    {
        var minPos = position - radius;
        var maxPos = position + radius;
        var nearbyContainers = new List<BlockEntityContainer>();
        world.BlockAccessor.WalkBlocks(minPos, maxPos, (_, x, y, z) =>
        {
            var be = world.BlockAccessor.GetBlockEntity(new BlockPos(x, y, z));

            if (be is BlockEntityGenericTypedContainer container)
            {
                if (!QuickStackChestTypes.Contains((container.InventoryClassName, container.type)))
                {
                    world.Logger.Debug(
                        $"[StorageTweaks] Skipped typed container for quick store nearby since it is not in the white list: (\"{container.inventoryClassName}\", \"{container.type}\")");
                    return;
                }

                nearbyContainers.Add(container);
                return;
            }

            if (be is not BlockEntityContainer bc)
            {
                return;
            }

            if (!QuickStackChestTypes.Contains((bc.InventoryClassName,
                    bc.Block.Code.FirstCodePart())))
            {
                world.Logger.Debug(
                    $"[StorageTweaks] Skipped container for quick store nearby since it is not in the white list: (\"{bc.InventoryClassName}\", \"{bc.Block.Code.FirstCodePart()}\"),");
                return;
            }

            nearbyContainers.Add(bc);
        }, true);

        return nearbyContainers;
    }

    public static void HandleQuickStoreNearbyContainers(
        IServerPlayer fromPlayer,
        QuickStoreNearbyContainersPacket packet
    )
    {
        var logger = fromPlayer.Entity.Api.Logger;
        // In 1.22 Entity.Pos changed from a field to a property
        var pos = Util.TryGetFieldOrProperty<EntityPos>(fromPlayer.Entity, "Pos");

        if (pos is null)
        {
            logger.Error("[StorageTweaks] Failed to get entity position from player");
            return;
        }

        var nearbyContainers = GetNearbyContainers(
            fromPlayer.Entity.World,
            pos.AsBlockPos,
            StorageTweaksModSystem.GetServerConfig().QuickStoreNearbySearchRadius
        );

        foreach (var container in nearbyContainers)
        {
            StorageTweaksModSystem.UnloadInventory(fromPlayer, container.Inventory, packet.StackPerishables);
            // MarkDirty(true) forces mesh re-tessellation on clients - required for BlockEntityDisplay
            // subclasses (FoodShelves, Purposeful Storage) that render their contents in the world.
            container.MarkDirty(true);
        }
    }
}
