using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;

namespace StorageTweaks.Extensions;

public static class ItemSlotExt
{
    private static readonly string[] SlotTypes =
    [
        // vanilla slots
        "ItemSlotSurvival",
        "ItemSlotBagContent",

        // for overhaullib before 1.22, Quivers And Sheaths and Backpacks mod use this slot type before 1.22
        "ItemSlotBagContentWithWildcardMatch",

        // for https://mods.vintagestory.at/playerinventorylib used by backpacks mod in 1.22+
        "BackpackSlot",

        // for https://mods.vintagestory.at/playerinventorylib without the Backpacks mod
        "VanillaBagContentSlot",

        // for https://mods.vintagestory.at/moreinventorys crates/baskets use these slots
        "ItemSlotDynamic",
        "StandardSlot",
    ];

    // slot class names that can be sorted into but maybe not out of unless also in the StorageTweaksModSystem.SlotTypes
    private static readonly HashSet<string> TargetWhitelist =
    [
        // tool-strap slot from https://mods.vintagestory.at/modularbackpacks
        "ItemSlotToolBagContent",
    ];

    // Check if the slots is a restricted slot from quivers and sheaths
    // technically it checks if it's a wildcard slot from overhaullib pre 1.22
    // or https://mods.vintagestory.at/overhaulliblegacycompat that does not use "*" as the wildcard
    public static bool IsRestrictedWildcardSlot(this ItemSlot slot)
    {
        var slotType = slot.GetType();
        if (slotType.Name != "ItemSlotBagContentWithWildcardMatch")
        {
            return false;
        }

        var configProp = slotType.GetProperty("Config");
        if (configProp?.GetValue(slot) is not { } config)
        {
            return false;
        }

        var canHoldWildcardsProp = config.GetType().GetProperty("CanHoldWildcards");
        if (canHoldWildcardsProp?.GetValue(config) is not IEnumerable<string> wildcards)
        {
            return false;
        }

        return !wildcards.Contains("*");
    }

    // slots that sort is allowed to move items out of
    public static bool CanSort(this ItemSlot slot)
    {
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (slot.IsRestrictedWildcardSlot())
        {
            return false;
        }

        return SlotTypes.Contains(slot.GetType().Name) && (slot.CanTake() || slot.Empty);
    }

    // returns true if the slot can be used to place items into after sorting
    public static bool CanSortInto(this ItemSlot slot)
    {
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (slot.IsRestrictedWildcardSlot())
        {
            return true;
        }

        return slot.CanSort() || TargetWhitelist.Contains(slot.GetType().Name);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public static OverhaullibLegacyInfo? GetOverhaullibLegacyInfo(this ItemSlot slot)
    {
        return OverhaullibLegacyInfo.New(slot);
    }

    // ReSharper disable once UnusedMember.Global
    public static bool IsQuiversAndSheathsSlot(this ItemSlot slot)
    {
        var info = slot.GetOverhaullibLegacyInfo();

        return info?.ToolBagId()?.StartsWith("quiversandsheaths:") ?? false;
    }
}

public class OverhaullibLegacyInfo
{
    private readonly ItemSlot slot;

    private OverhaullibLegacyInfo(ItemSlot slot)
    {
        this.slot = slot;
    }

    public static OverhaullibLegacyInfo? New(ItemSlot slot)
    {
        return slot.GetType().Name != "ItemSlotBagContentWithWildcardMatch" ? null : new OverhaullibLegacyInfo(slot);
    }

    // ReSharper disable once UnusedMember.Global
    public string? BackpackCategoryCode()
    {
        try
        {
            var propInfo = AccessTools.Property(slot.GetType(), "BackpackCategoryCode");
            return (string?)propInfo.GetValue(slot);
        }
        catch (Exception e)
        {
            StorageTweaksModSystem.Logger().Error($"[StorageTweaks] Failed to get BackpackCategoryCode: {e}");
            return null;
        }
    }

    // ReSharper disable once UnusedMember.Global
    public float? OrderPriority()
    {
        try
        {
            var propInfo = AccessTools.Property(slot.GetType(), "OrderPriority");
            return (float?)propInfo.GetValue(slot);
        }
        catch (Exception e)
        {
            StorageTweaksModSystem.Logger().Error($"[StorageTweaks] Failed to get OrderPriority: {e}");
            return null;
        }
    }

    public string? ToolBagId()
    {
        try
        {
            var propInfo = AccessTools.Property(slot.GetType(), "ToolBagId");
            return (string?)propInfo.GetValue(slot);
        }
        catch (Exception e)
        {
            StorageTweaksModSystem.Logger().Error($"[StorageTweaks] Failed to get ToolBagId: {e}");
            throw;
        }
    }
}
