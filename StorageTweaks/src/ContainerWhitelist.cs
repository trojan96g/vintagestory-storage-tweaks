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
