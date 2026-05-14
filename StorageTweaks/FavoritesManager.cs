using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace StorageTweaks;

public class FavoritesManager
{
    public const string FavoritesKey = "storageTweaksFavorites";
    private static FavoritesManager? instance;
    private readonly ICoreClientAPI capi;
    private readonly IClientNetworkChannel networkChannel;

    private FavoritesManager(ICoreClientAPI capi)
    {
        this.capi = capi;
        networkChannel = capi.Network.GetChannel("storagetweaks");
    }

    public bool IsFavoriteModeActive { get; set; }

    public static void Initialize(ICoreClientAPI capi)
    {
        instance = new FavoritesManager(capi);
    }

    public static FavoritesManager? Get()
    {
        return instance;
    }

    public bool IsFavorite(ItemStack stack)
    {
        var tree = capi.World.Player?.Entity?.WatchedAttributes;
        if (tree == null || stack.Collectible?.Code == null) return false;

        var favoritesAttr = tree.GetTreeAttribute(FavoritesKey);
        return favoritesAttr?.GetAsBool(GetItemKey(stack)) ?? false;
    }

    public void ToggleFavorite(ItemStack stack)
    {
        if (stack.Collectible?.Code == null) return;

        var key = GetItemKey(stack);
        var newState = !IsFavorite(stack);

        var tree = capi.World.Player?.Entity?.WatchedAttributes;
        if (tree != null)
        {
            var favoritesAttr = tree.GetTreeAttribute(FavoritesKey);
            if (favoritesAttr == null)
            {
                favoritesAttr = new TreeAttribute();
                tree[FavoritesKey] = favoritesAttr;
            }

            if (newState) favoritesAttr.SetBool(key, newState);
            else favoritesAttr.RemoveAttribute(key);
        }

        networkChannel.SendPacket(new UpdateFavoritesPacket { Code = key, IsFavorite = newState });
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