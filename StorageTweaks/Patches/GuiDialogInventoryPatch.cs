// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace StorageTweaks.Patches;

[HarmonyPatch]
public class GuiDialogInventoryPatch
{
    [HarmonyPatch(typeof(GuiDialogInventory), "ComposeSurvivalInvDialog")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void ComposeSurvivalInvDialog(GuiDialogInventory __instance)
    {
        var composer =  GetComposer(__instance);
        var capi = composer.Api!;
        capi.Logger.Debug("[StorageTweaks] Composing inventory action buttons.");
        
        var modSystem = composer.Api.ModLoader.GetModSystem<StorageTweaksModSystem>();

        if (modSystem == null)
        {
            composer.Api.Logger.Warning("[StorageTweaks] StorageTweaksModSystem not found for gui dialog inventory");
            return;
        }

        modSystem.InventoryActionButtons!.ComposeGui(composer);
    }
    
    [HarmonyPatch(typeof(GuiDialogInventory), "OnGuiClosed")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void OnGuiClosed(GuiDialogInventory __instance)
    {
        var composer =  GetComposer(__instance);
        var modSystem = composer.Api.ModLoader.GetModSystem<StorageTweaksModSystem>();
        if (modSystem.FavoritesManager == null) return;
        modSystem.FavoritesManager.IsFavoriteModeActive = false;
    }

    private static GuiComposer GetComposer(GuiDialogInventory inventoryDialog)
    {
        
        var field = inventoryDialog.GetType().GetField("survivalInvDialog", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var composer = (GuiComposer)field.GetValue(inventoryDialog)!;
        return composer;
    }
}