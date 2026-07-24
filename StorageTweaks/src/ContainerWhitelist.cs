using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace StorageTweaks;

/// <summary>
///     Container inclusion/exclusion system for the "quick store nearby" feature.
/// </summary>
public static class ContainerWhitelist
{
    private static readonly HashSet<AssetLocation> BuiltInWhitelist =
    [
        // vanilla containers
        "game:chest-*",
        "game:crate",
        "game:labeledchest-*",
        // String Sense mod uses game:stationarybasket as well
        "game:stationarybasket-*",
        "game:storagevessel-*",
        "game:trunk-*",

        // Better Crates mod
        "bettercrates:bettercrate-bronze-center-*",
        "bettercrates:bettercrate-copper-center-*",
        "bettercrates:bettercrate-iron-center-*",
        "bettercrates:bettercrate-steel-center-*",
        "bettercrates:bettercrate-wood-center-*",
        "bettercrates:bettercrate2sided-bronze-center-*",
        "bettercrates:bettercrate2sided-copper-center-*",
        "bettercrates:bettercrate2sided-iron-center-*",
        "bettercrates:bettercrate2sided-steel-center-*",
        "bettercrates:bettercrate2sided-wood-center-*",

        // Extra Chests mod
        "extrachests:labeledchest-*",
        "extrachests:chest-*",

        // Containers Bundle mod
        "containersbundle:bamboochest-*",
        "containersbundle:cupboardnolabel-*",
        "containersbundle:cupboardwithlabel-*",
        "containersbundle:exquisitechest-*",
        "containersbundle:foodcupboard-*",
        "containersbundle:foodcupboardwall-*",
        "containersbundle:linencrate-*",
        "containersbundle:longcrate-*",
        "containersbundle:metalcabinetnolabel-*",
        "containersbundle:stonecasket-*",
        "containersbundle:strongbox-*",
        "containersbundle:wickerbasket-*",
        "containersbundle:woodenbox-*",

        // Food Shelves mod
        // "foodshelves:barrelrack-normal-*",
        // "foodshelves:barrelrack-top-*",
        "foodshelves:breadshelf-normal-*",
        "foodshelves:ceilingrack-normal",
        "foodshelves:coolingcabinet-normal-*",
        "foodshelves:doubleshelf-normal-*",
        "foodshelves:eggbasket-normal",
        "foodshelves:eggshelf-normal-*",
        "foodshelves:eggshelf-short-*",
        "foodshelves:floursack-normal-*",
        "foodshelves:fooddisplayblock-*",
        "foodshelves:fooddisplayblock-top",
        "foodshelves:fooddisplaycase-normal-*",
        "foodshelves:fruitbasket-normal",
        "foodshelves:fruitcooler-normal-*",
        "foodshelves:jar-normal",
        "foodshelves:jarlarge-normal",
        "foodshelves:jarstand-normal-*",
        "foodshelves:meatfreezer-normal-*",
        "foodshelves:mushroombasket-normal",
        "foodshelves:pieshelf-normal-*",
        "foodshelves:pumpkincase-normal",
        "foodshelves:seedbins-normal-*",
        "foodshelves:seedshelf-normal-*",
        "foodshelves:seedshelf-short-*",
        "foodshelves:shortshelf-normal-*",
        "foodshelves:tablewshelf-normal-*",
        // "foodshelves:tunrack-normal-*",
        // "foodshelves:tunrack-top-*",
        "foodshelves:vegetablebasket-normal",
        "foodshelves:wallcabinet-normal-*",
    ];

    private static readonly HashSet<AssetLocation> Whitelist = [];

    public static void InitWhitelist(ICoreServerAPI api)
    {
        var stopwatch = Stopwatch.StartNew();
        Whitelist.Clear();

        var serverConfig = StorageTweaksModSystem.GetServerConfig();
        var registry = api.World.ClassRegistry;

        // Pre-build the AssetLocation needles once, outside the per-collectible loop, so we
        // don't allocate a fresh AssetLocation for every (collectible x needle) pair.
        var builtinWhitelist = BuiltInWhitelist
            .ToArray();
        var blacklist = serverConfig.ContainerBlacklist
            .Where(needle => !string.IsNullOrWhiteSpace(needle))
            .Select(needle => new AssetLocation(needle))
            .ToArray();
        var additionalWhitelist = serverConfig.AdditionalContainerWhitelist
            .Where(needle => !string.IsNullOrWhiteSpace(needle))
            .Select(needle => new AssetLocation(needle))
            .ToArray();

        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var item in api.World.Collectibles)
        {
            var code = item.Code;
            if (code is null)
            {
                continue;
            }

            // Skip non block items up front
            if (item is not Block block)
            {
                continue;
            }

            var entityClass = block.EntityClass;
            if (string.IsNullOrEmpty(entityClass))
            {
                continue;
            }

            // early discard none container item types
            var blockEntityType = registry.GetBlockEntity(entityClass);
            if (blockEntityType == null || !typeof(IBlockEntityContainer).IsAssignableFrom(blockEntityType))
            {
                continue;
            }

            if (blacklist.Any(needle => WildcardUtil.Match(needle, code)))
            {
                continue;
            }

            if (BuiltInWhitelist.Contains(code) || builtinWhitelist.Any(needle => WildcardUtil.Match(needle, code)))
            {
                Whitelist.Add(code);
                continue;
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (additionalWhitelist.Any(needle => WildcardUtil.Match(needle, code)))
            {
                Whitelist.Add(code);
            }
        }

        var codes = string.Join(Environment.NewLine, Whitelist.Select(code => code.ToString()));
        stopwatch.Stop();
        var duration = stopwatch.ElapsedMilliseconds;
        api.Logger.VerboseDebug($"[StorageTweaks] Store nearby whitelist initialized with {Whitelist.Count} items in {duration} ms:\n{codes}");
    }

    /// <summary>
    ///     Returns <c>true</c> when the given container <see cref="Block.Code" /> is allowed by the
    ///     built-in and additional whitelists and not excluded by the blacklist. Blacklist entries take
    ///     precedence over both whitelist sources. The additional whitelist and blacklist are read
    ///     directly from <see cref="StorageTweaksModSystem.GetServerConfig" />.
    /// </summary>
    public static bool IsAllowed(AssetLocation code)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        return code != null && Whitelist.Contains(code);
    }
}
