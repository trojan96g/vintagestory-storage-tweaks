using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
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
        "game:chest-east",
        "game:chest-north",
        "game:chest-south",
        "game:chest-west",
        "game:crate",
        "game:labeledchest-east",
        "game:labeledchest-north",
        "game:labeledchest-south",
        "game:labeledchest-west",
        // String Sense mod uses game:stationarybasket as well
        "game:stationarybasket-east",
        "game:stationarybasket-north",
        "game:stationarybasket-south",
        "game:stationarybasket-west",
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
        "game:trunk-east",
        "game:trunk-north",
        "game:trunk-south",
        "game:trunk-west",

        // Better Crates mod
        "bettercrates:bettercrate-bronze-center-east",
        "bettercrates:bettercrate-bronze-center-north",
        "bettercrates:bettercrate-bronze-center-south",
        "bettercrates:bettercrate-bronze-center-west",
        "bettercrates:bettercrate-copper-center-east",
        "bettercrates:bettercrate-copper-center-north",
        "bettercrates:bettercrate-copper-center-south",
        "bettercrates:bettercrate-copper-center-west",
        "bettercrates:bettercrate-iron-center-east",
        "bettercrates:bettercrate-iron-center-north",
        "bettercrates:bettercrate-iron-center-south",
        "bettercrates:bettercrate-iron-center-west",
        "bettercrates:bettercrate-steel-center-east",
        "bettercrates:bettercrate-steel-center-north",
        "bettercrates:bettercrate-steel-center-south",
        "bettercrates:bettercrate-steel-center-west",
        "bettercrates:bettercrate-wood-center-east",
        "bettercrates:bettercrate-wood-center-north",
        "bettercrates:bettercrate-wood-center-south",
        "bettercrates:bettercrate-wood-center-west",
        "bettercrates:bettercrate2sided-bronze-center-east",
        "bettercrates:bettercrate2sided-bronze-center-north",
        "bettercrates:bettercrate2sided-bronze-center-south",
        "bettercrates:bettercrate2sided-bronze-center-west",
        "bettercrates:bettercrate2sided-copper-center-east",
        "bettercrates:bettercrate2sided-copper-center-north",
        "bettercrates:bettercrate2sided-copper-center-south",
        "bettercrates:bettercrate2sided-copper-center-west",
        "bettercrates:bettercrate2sided-iron-center-east",
        "bettercrates:bettercrate2sided-iron-center-north",
        "bettercrates:bettercrate2sided-iron-center-south",
        "bettercrates:bettercrate2sided-iron-center-west",
        "bettercrates:bettercrate2sided-steel-center-east",
        "bettercrates:bettercrate2sided-steel-center-north",
        "bettercrates:bettercrate2sided-steel-center-south",
        "bettercrates:bettercrate2sided-steel-center-west",
        "bettercrates:bettercrate2sided-wood-center-east",
        "bettercrates:bettercrate2sided-wood-center-north",
        "bettercrates:bettercrate2sided-wood-center-south",
        "bettercrates:bettercrate2sided-wood-center-west",

        // Extra Chests mod
        "extrachests:labeledchest-east",
        "extrachests:labeledchest-north",
        "extrachests:labeledchest-south",
        "extrachests:labeledchest-west",
        "extrachests:chest-east",
        "extrachests:chest-north",
        "extrachests:chest-south",
        "extrachests:chest-west",
    ];

    /// <summary>
    ///     Returns <c>true</c> when the given container <see cref="Block.Code" /> is allowed by the
    ///     built-in and additional whitelists and not excluded by the blacklist. Blacklist entries take
    ///     precedence over both whitelist sources. The additional whitelist and blacklist are read
    ///     directly from <see cref="StorageTweaksModSystem.GetServerConfig" />.
    /// </summary>
    public static bool IsAllowed(AssetLocation code)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (code == null)
        {
            return false;
        }

        var serverConfig = StorageTweaksModSystem.GetServerConfig();
        if (MatchesAny(serverConfig.ContainerBlacklist, code))
        {
            return false;
        }

        return BuiltInWhitelist.Contains(code) ||
               MatchesAny(serverConfig.AdditionalContainerWhitelist, code);
    }

    private static bool MatchesAny(IEnumerable<string>? patterns, AssetLocation code)
    {
        if (patterns == null)
        {
            return false;
        }

        return patterns.Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Any(pattern => WildcardUtil.Match(new AssetLocation(pattern), code));
    }
}
