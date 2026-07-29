// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using HarmonyLib;
using StorageTweaks.Extensions;
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

        // increase priority of toolstrap slots from Modular Backpacks mod
        if (slotType.Name == "ItemSlotToolBagContent")
        {
            __result += 1.0f;
            return;
        }

        if (targetSlot.IsRestrictedWildcardSlot())
        {
            __result += 1.0f;
        }
    }
}
