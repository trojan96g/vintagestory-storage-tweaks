using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace StorageTweaks;

public static class QuickStoreNearbyContainerSystem
{
    private const int SearchRadius = 8;

    // Chest types to include when looking for nearby chests to quick deposit matching items from inventory
    private static readonly HashSet<(string, string)> QuickStackChestTypes =
    [
        ("basket", "reed"),
        ("basket", "papyrus"),
        ("chest", "normal-labeled"),
        ("chest", "normal-generic"),
        ("chest", "normal")
    ];

    private static List<BlockEntityGenericTypedContainer> GetNearbyContainers(IWorldAccessor world, BlockPos position,
        int radius)
    {
        var minPos = position - radius;
        var maxPos = position + radius;
        var nearbyContainers = new List<BlockEntityGenericTypedContainer>();
        world.BlockAccessor.WalkBlocks(minPos, maxPos, (_, x, y, z) =>
        {
            var be = world.BlockAccessor.GetBlockEntity(new BlockPos(x, y, z));
            if (be is not BlockEntityGenericTypedContainer container) return;
            if (!QuickStackChestTypes.Contains((container.InventoryClassName, container.type))) return;
            nearbyContainers.Add(container);
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
            SearchRadius
        );

        foreach (var container in nearbyContainers)
        {
            StorageTweaksModSystem.UnloadInventory(fromPlayer, container.Inventory, packet.StackPerishables);
        }
    }
}