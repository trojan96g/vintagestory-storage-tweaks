using System.Collections.Generic;
using System.Reflection;
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
            world.Logger.VerboseDebug(
                $"[StorageTweaks] Quick store nearby {(allowed ? "matched" : "skipped")} container \"{code}\"");
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

        // Collect positions of Food Shelves containers (e.g. Ceiling Rack) whose baked-in
        // blockMesh needs explicit InitMesh on the client after unload. MarkDirty(true)
        // only nulls tfMatrices; it does not rebuild blockMesh. See RemeshContainersPacket.
        var remeshPositions = new List<BlockPos>();

        foreach (var container in nearbyContainers)
        {
            StorageTweaksModSystem.UnloadInventory(fromPlayer, container.Inventory, packet.StackPerishables);
            // MarkDirty(true) forces mesh re-tessellation on clients - required for BlockEntityDisplay
            // subclasses (Food Shelves, Purposeful Storage) that render their contents in the world.
            container.MarkDirty(true);

            if (NeedsRemesh(container))
            {
                remeshPositions.Add(container.Pos.Copy());
            }
        }

        if (remeshPositions.Count > 0)
        {
            ((IServerNetworkChannel)fromPlayer.Entity.Api.Network.GetChannel("storagetweaks"))
                .SendPacket(new RemeshContainersPacket { Positions = remeshPositions }, fromPlayer);
        }
    }

    /// <summary>
    ///     True when <paramref name="container" /> is a <see cref="BlockEntityDisplay" /> subclass
    ///     that declares an <c>InitMesh()</c> method - the Food Shelves pattern where the content
    ///     mesh is cached in <c>blockMesh</c> and only refreshed by the mod's own code.
    /// </summary>
    private static bool NeedsRemesh(BlockEntityContainer container)
    {
        if (container is not BlockEntityDisplay)
        {
            return false;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                                   BindingFlags.DeclaredOnly;
        for (var type = container.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            var method = type.GetMethod("InitMesh", flags);
            if (method != null && method.GetParameters().Length == 0)
            {
                return true;
            }
        }

        return false;
    }
}
