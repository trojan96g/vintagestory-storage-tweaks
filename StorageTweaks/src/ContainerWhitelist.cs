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
    private static readonly HashSet<string> BuiltInWhitelist =
    [
        // vanilla containers
        "game:chest-*",
        "game:crate",
        "game:labeledchest-*",
        // String Sense mod uses game:stationarybasket as well
        "game:stationarybasket-*",
        "game:storagevessel-abyss",
        "game:storagevessel-ashforest",
        "game:storagevessel-beehive",
        "game:storagevessel-black-fired",
        "game:storagevessel-blue-fired",
        "game:storagevessel-brown-fired",
        "game:storagevessel-caveaurora",
        "game:storagevessel-chains",
        "game:storagevessel-chthonic",
        "game:storagevessel-cloisonne",
        "game:storagevessel-collonade",
        "game:storagevessel-copper",
        "game:storagevessel-cornflower",
        "game:storagevessel-cowrie",
        "game:storagevessel-cream-fired",
        "game:storagevessel-earthen",
        "game:storagevessel-earthyorange-fired",
        "game:storagevessel-entrenched",
        "game:storagevessel-fire-fired",
        "game:storagevessel-golden",
        "game:storagevessel-gray-fired",
        "game:storagevessel-harvest",
        "game:storagevessel-honeydew",
        "game:storagevessel-loam",
        "game:storagevessel-motheaten",
        "game:storagevessel-orange-fired",
        "game:storagevessel-oxblood",
        "game:storagevessel-patina",
        "game:storagevessel-pine",
        "game:storagevessel-rain",
        "game:storagevessel-rattlesnake",
        "game:storagevessel-red-fired",
        "game:storagevessel-rime",
        "game:storagevessel-rutile",
        "game:storagevessel-seasalt",
        "game:storagevessel-serpents",
        "game:storagevessel-springflowers",
        "game:storagevessel-talik",
        "game:storagevessel-tan-fired",
        "game:storagevessel-void",
        "game:storagevessel-volcanic",
        "game:storagevessel-waves",
        "game:storagevessel-wintersea",
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
        "containersbundle:metalcabinetnolabel-*-bismuthbronze",
        "containersbundle:metalcabinetnolabel-*-brass",
        "containersbundle:metalcabinetnolabel-*-copper",
        "containersbundle:metalcabinetnolabel-*-electrum",
        "containersbundle:metalcabinetnolabel-*-gold",
        "containersbundle:metalcabinetnolabel-*-iron",
        "containersbundle:metalcabinetnolabel-*-lead",
        "containersbundle:metalcabinetnolabel-*-meteoriciron",
        "containersbundle:metalcabinetnolabel-*-nickel",
        "containersbundle:metalcabinetnolabel-*-platinum",
        "containersbundle:metalcabinetnolabel-*-silver",
        "containersbundle:metalcabinetnolabel-*-steel",
        "containersbundle:metalcabinetnolabel-*-tin",
        "containersbundle:metalcabinetnolabel-*-tinbronze",
        "containersbundle:metalcabinetnolabel-*-titanium",
        "containersbundle:metalcabinetnolabel-*-zinc",
        "containersbundle:stonecasket-*",
        "containersbundle:strongbox-*-bismuthbronze",
        "containersbundle:strongbox-*-blackbronze",
        "containersbundle:strongbox-*-tinbronze",
        "containersbundle:wickerbasket-*",
        "containersbundle:woodenbox-*",
    ];

    private static readonly HashSet<AssetLocation> Whitelist = [];

    public static void InitWhitelist(ICoreServerAPI api)
    {
        var stopwatch = Stopwatch.StartNew();
        Whitelist.Clear();
        var builtinWhitelist = BuiltInWhitelist.ToArray();
        var serverConfig = StorageTweaksModSystem.GetServerConfig();
        var additionalContainerWhitelist = serverConfig.AdditionalContainerWhitelist;
        var blacklist = serverConfig.ContainerBlacklist;

        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var item in api.World.Collectibles)
        {
            var code = item.Code;
            if (code is null)
            {
                continue;
            }

            if (blacklist.Any(needle => WildcardUtil.Match(new AssetLocation(needle), code)))
            {
                continue;
            }

            if (BuiltInWhitelist.Contains(code) || builtinWhitelist.Any(needle => WildcardUtil.Match(new AssetLocation(needle), code)))
            {
                Whitelist.Add(code);
                continue;
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (additionalContainerWhitelist.Where(needle => needle != null && needle.Trim().Length > 0)
                .Any(needle => WildcardUtil.Match(new AssetLocation(needle), code)))
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
