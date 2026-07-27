// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace StorageTweaks.Patches;

/// <summary>
///     Adds a custom "Containers" tab to the creative inventory that lists every container block.
///     Useful for discovering and testing the containers that the quick store nearby feature should consider.
/// </summary>
[HarmonyPatch(typeof(InventoryPlayerCreative), "GatherTabStacks")]
public static class CreativeInventoryContainerTabPatch
{
    /// <summary>
    ///     The vanilla "Everything" tab code, used as the source list to filter down.
    /// </summary>
    private const string EverythingTabCode = "general";

    /// <summary>
    ///     The custom creative tab code, also used as the <c>tabname-{Code}</c> lang key suffix.
    /// </summary>
    private const string TabCode = "storage-containers";

    [HarmonyPostfix]
    // ReSharper disable InconsistentNaming
    public static void Postfix(
        ref Dictionary<string, List<ItemStack>> __result,
        ref InventoryPlayerCreative __instance)
    {
        // Filter the already-built "Everything" (general) tab down to container blocks. If the general
        // tab is absent (e.g. stripped out by another mod) there is nothing to mirror, so skip.
        if (!__result.TryGetValue(EverythingTabCode, out var everythingStacks))
        {
            return;
        }

        var registry = __instance.Api?.World?.ClassRegistry;
        if (registry == null)
        {
            return;
        }

        var containerStacks = new List<ItemStack>();
        foreach (var stack in everythingStacks)
        {
            if (stack.Collectible is not Block block)
            {
                continue;
            }

            var entityClass = block.EntityClass;
            if (string.IsNullOrEmpty(entityClass))
            {
                continue;
            }

            var blockEntityType = registry.GetBlockEntity(entityClass);
            if (blockEntityType == null || !typeof(IBlockEntityContainer).IsAssignableFrom(blockEntityType))
            {
                continue;
            }

            // Clone so the tab's slot inventory does not share ItemStack instances with the "Everything"
            // tab's slot inventory. (Vintage Story's creative stacks are already clones when stored, but
            // reusing them across two tabs would let both inventories hold the same instance.)
            containerStacks.Add(stack.Clone());
        }

        if (containerStacks.Count > 0)
        {
            __result.TryAdd(TabCode, containerStacks);
        }
    }
}