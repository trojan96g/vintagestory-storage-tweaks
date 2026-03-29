using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace StorageTweaks;

public class FavoritesManager
{
    private static FavoritesManager? _instance;
    public const string FavoritesKey = "storageTweaksFavorites";
    private readonly IClientNetworkChannel _networkChannel;
    private readonly ICoreClientAPI _capi;

    private FavoritesManager(ICoreClientAPI capi)
    {
        _capi = capi;
        _networkChannel = capi.Network.GetChannel("storagetweaks");
    }

    public static void Initialize(ICoreClientAPI capi)
    {
        _instance = new FavoritesManager(capi);
    }

    public static FavoritesManager? Get()
    {
        return _instance;
    }

    public bool IsFavoriteModeActive { get; set; }

    public bool IsFavorite(ItemStack stack)
    {
        var tree = _capi.World.Player?.Entity?.WatchedAttributes;
        if (tree == null || stack.Collectible?.Code == null) return false;

        var favoritesAttr = tree.GetTreeAttribute(FavoritesKey);
        return favoritesAttr?.GetAsBool(GetItemKey(stack)) ?? false;
    }

    public void ToggleFavorite(ItemStack stack)
    {
        if (stack.Collectible?.Code == null) return;

        var key = GetItemKey(stack);
        var newState = !IsFavorite(stack);

        var tree = _capi.World.Player?.Entity?.WatchedAttributes;
        if (tree != null)
        {
            var favoritesAttr = tree.GetTreeAttribute(FavoritesKey);
            if (favoritesAttr == null)
            {
                favoritesAttr = new TreeAttribute();
                tree[FavoritesKey] = favoritesAttr;
            }

            if (newState)
            {
                favoritesAttr.SetBool(key, newState);
            }
            else
            {
                favoritesAttr.RemoveAttribute(key);
            }
        }

        _networkChannel.SendPacket(new UpdateFavoritesPacket { Code = key, IsFavorite = newState });
    }

    public int GetFavoriteCount()
    {
        var tree = _capi.World.Player?.Entity?.WatchedAttributes;
        return tree?.GetTreeAttribute(FavoritesKey)?.Count ?? 0;
    }

    private static string GetItemKey(ItemStack stack)
    {
        return stack.Collectible.Code.ToString();
    }

    public static bool IsFavorite(IPlayer player, ItemStack stack)
    {
        return player.Entity.WatchedAttributes.GetTreeAttribute(FavoritesKey)?.GetAsBool(GetItemKey(stack)) ?? false;
    }
}