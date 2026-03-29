// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using HarmonyLib;
using Vintagestory.Client.NoObf;

namespace StorageTweaks;

[HarmonyPatch(typeof(GuiDialogInventory), "OnGuiClosed")]
public class GuiDialogInventoryPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        FavoritesManager.Get()!.IsFavoriteModeActive = false;
    }
}