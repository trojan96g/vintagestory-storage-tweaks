// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using System.Reflection;
using HarmonyLib;
using StorageTweaks.Gui;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace StorageTweaks.Patches;

[HarmonyPatch]
public class GuiDialogInventoryPatch
{
    private static InventoryActionButtons? actionButtons;
    
    [HarmonyPatch(typeof(GuiDialogInventory), "ComposeSurvivalInvDialog")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void ComposeSurvivalInvDialog(GuiDialogInventory __instance)
    {
        var field = __instance.GetType().GetField("survivalInvDialog", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var composer = (GuiComposer)field.GetValue(__instance)!;
        var capi = composer.Api!;
        capi.Logger.Debug("[StorageTweaks] Composing inventory action buttons.");

        actionButtons ??= new InventoryActionButtons(capi);
        
        actionButtons.ComposeGui(composer);
    }
    
    [HarmonyPatch(typeof(GuiDialogInventory), "OnGuiClosed")]
    [HarmonyPostfix]
    public static void OnGuiClosed()
    {
        FavoritesManager.Get()!.IsFavoriteModeActive = false;
    }
}