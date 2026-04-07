// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;

namespace StorageTweaks.Patches;

/// <summary>
///     Increase the suitability of bag slots that don't have the wildcard `*`.
///     This is to decrease the priority of the "Backpacks" mod slots
///     and increase the priority of the "Quivers and Sheaths" slots.
///     See: https://mods.vintagestory.at/storagetweaks#cmt-147321
/// </summary>
[HarmonyPatch(typeof(InventoryBase), "GetSuitability")]
public class InventoryBasePatch
{
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    // ReSharper disable UnusedParameter.Global
    public static void Postfix(ref float __result, ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
    {
        var slotType = targetSlot.GetType();
        if (slotType.Name != "ItemSlotBagContentWithWildcardMatch") return;

        var configProp = slotType.GetProperty("Config");
        if (configProp?.GetValue(targetSlot) is not { } config)
            return;

        var canHoldWildcardsProp = config.GetType().GetProperty("CanHoldWildcards");
        if (canHoldWildcardsProp?.GetValue(config) is not IEnumerable<string> wildcards)
            return;

        if (!wildcards.Contains("*")) __result += 1.0f;
    }
}