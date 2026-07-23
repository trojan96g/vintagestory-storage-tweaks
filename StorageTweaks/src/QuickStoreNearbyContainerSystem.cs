using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace StorageTweaks;

public static class QuickStoreNearbyContainerSystem
{
    private static List<BlockEntityContainer> GetNearbyContainers(IWorldAccessor world, BlockPos position,
        int radius)
    {
        var minPos = position - radius;
        var maxPos = position + radius;
        var nearbyContainers = new List<BlockEntityContainer>();
        world.BlockAccessor.WalkBlocks(minPos, maxPos, (_, x, y, z) =>
        {
            if (world.BlockAccessor.GetBlockEntity(new BlockPos(x, y, z)) is not BlockEntityContainer bc)
            {
                return;
            }

            var code = bc.Block.Code;
            var allowed = ContainerWhitelist.IsAllowed(code);
            world.Logger.Debug(
                $"[StorageTweaks] Quick store nearby {(allowed ? "matched" : "skipped")} container \"{code}\" at {x},{y},{z}");
            if (!allowed)
            {
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