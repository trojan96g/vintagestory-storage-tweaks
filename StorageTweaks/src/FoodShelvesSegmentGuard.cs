using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;

namespace StorageTweaks;

/// <summary>
///     Reflection bridge to the Food Shelves mod's <c>BEBaseFSContainer</c> segment-limit
///     system. Food Shelves caps how many items may sit in one *segment* (a run of
///     contiguous <c>ItemsPerSegment</c> slots) based on the item's size category - see
///     <c>SegmentLimits.Mixed</c>. The vanilla <c>GetBestSuitedSlot</c>/<c>TryPutInto</c>
///     path used by our unload only respects per-slot <c>MaxSlotStackSize</c>, so without
///     this guard we overstuff segments (e.g. a Cooling Cabinet segment that should hold
///     at most 2 medium items ends up with 24). Resolved via reflection so this mod has
///     no hard dependency on the Food Shelves assembly; the guard stays disabled when
///     Food Shelves isn't loaded.
/// </summary>
internal sealed class FoodShelvesSegmentGuard
{
    private readonly BlockEntity be;
    private readonly MethodInfo countItemsInSegment;
    private readonly MethodInfo getSegmentLimit;
    private readonly int itemsPerSegment;

    private FoodShelvesSegmentGuard(BlockEntity be, int itemsPerSegment,
        MethodInfo getSegmentLimit, MethodInfo countItemsInSegment)
    {
        this.be = be;
        this.itemsPerSegment = itemsPerSegment;
        this.getSegmentLimit = getSegmentLimit;
        this.countItemsInSegment = countItemsInSegment;
    }

    public bool Enabled
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        get => be != null;
    }

    private static FoodShelvesSegmentGuard Disabled { get; } = new(null!, 0, null!, null!);

    /// <summary>
    ///     Builds a guard for the given inventory if it belongs to a "Food Shelves"
    ///     <c>BEBaseFSContainer</c>, returns a disabled guard otherwise. Detection goes
    ///     through the <c>FoodShelves.IFoodShelvesContainer</c> interface rather than the
    ///     class name so future subclasses keep working.
    /// </summary>
    public static FoodShelvesSegmentGuard For(IInventory destInventory, IWorldAccessor world)
    {
        if (destInventory is not InventoryBase invBase || invBase.Pos == null)
        {
            return Disabled;
        }

        var be = world.BlockAccessor.GetBlockEntity(invBase.Pos);
        if (be == null)
        {
            return Disabled;
        }

        // IFoodShelvesContainer is the Food Shelves marker interface; resolve by name so we
        // don't need a reference to the Food Shelves assembly.
        var beType = be.GetType();
        var fsContainerInterface = beType.GetInterface("FoodShelves.IFoodShelvesContainer");
        if (fsContainerInterface == null)
        {
            return Disabled;
        }

        // ItemsPerSegment and CountItemsInSegment are `public virtual` on BEBaseFSContainer;
        // plain GetProperty/GetMethod returns the most-derived override. GetSegmentLimit
        // is protected, so NonPublic is required.
        var itemsPerSegmentProp = beType.GetProperty("ItemsPerSegment");
        var getSegmentLimitMethod = beType.GetMethod("GetSegmentLimit",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var countItemsInSegmentMethod = beType.GetMethod("CountItemsInSegment",
            BindingFlags.Instance | BindingFlags.Public);

        if (itemsPerSegmentProp == null || getSegmentLimitMethod == null || countItemsInSegmentMethod == null)
        {
            return Disabled;
        }

        var itemsPerSegment = (int)itemsPerSegmentProp.GetValue(be)!;
        if (itemsPerSegment <= 0)
        {
            return Disabled;
        }

        return new FoodShelvesSegmentGuard(be, itemsPerSegment,
            getSegmentLimitMethod, countItemsInSegmentMethod);
    }

    /// <summary>
    ///     Returns true when the Food Shelves segment containing <paramref name="destSlot" />
    ///     already holds <c>GetSegmentLimit(sourceStack)</c> items, i.e. no more items of
    ///     that stack's size category may be placed there. Bulk slots
    ///     (<c>ItemSlotFSUniversal</c> with <c>isBulk = true</c>, e.g. the Ceiling Rack jar
    ///     slot) bypass the cap - this mirrors <c>BEBaseFSContainer.TryPut</c>.
    /// </summary>
    public bool SegmentIsFull(ItemSlot destSlot, ItemStack sourceStack)
    {
        if (!Enabled)
        {
            return false;
        }

        // Bulk slots skip the Food Shelves segment cap. ItemSlotFSUniversal exposes
        // `isBulk` as a public readonly field.
        var destType = destSlot.GetType();
        if (destType.Name == "ItemSlotFSUniversal")
        {
            var isBulkField = destType.GetField("isBulk", BindingFlags.Public | BindingFlags.Instance);
            if (isBulkField != null && (bool)isBulkField.GetValue(destSlot)!)
            {
                return false;
            }
        }

        var slotIndex = destSlot.Inventory.GetSlotId(destSlot);
        if (slotIndex < 0)
        {
            return false;
        }

        var startIndex = slotIndex / itemsPerSegment * itemsPerSegment;
        var count = (int)countItemsInSegment.Invoke(be, [startIndex])!;
        var limit = (int)getSegmentLimit.Invoke(be, [sourceStack])!;
        return count >= limit;
    }

    /// <summary>
    ///     Returns the destination slots whose Food Shelves segments are already at the
    ///     cap for <paramref name="sourceStack" />. Used to seed the unload loop's
    ///     <c>ignoredSlots</c> so <c>GetBestSuitedSlot</c> skips full segments entirely.
    /// </summary>
    public HashSet<ItemSlot> FullSlotsFor(IInventory destInventory, ItemStack sourceStack)
    {
        var result = new HashSet<ItemSlot>();
        if (!Enabled)
        {
            return result;
        }

        foreach (var slot in destInventory)
        {
            if (SegmentIsFull(slot, sourceStack))
            {
                result.Add(slot);
            }
        }

        return result;
    }
}
