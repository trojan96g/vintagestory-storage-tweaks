// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace StorageTweaks.Patches;

[HarmonyPatch]
public class GuiDialogInventoryPatch
{
    [HarmonyPatch(typeof(GuiComposerHelpers), "AddDialogTitleBar")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void AddDialogTitleBar(GuiComposer composer)
    {
        var capi = composer.Api;

        if (capi == null)
        {
            Console.Error.WriteLine("[StorageTweaks] Couldn't find Api in GuiComposer");
            return;
        }

        if (composer.DialogName != "inventory-backpack") return;

        capi.Logger.Debug("[StorageTweaks] Composing inventory action buttons.");

        var modSystem = capi.ModLoader.GetModSystem<StorageTweaksModSystem>();

        if (modSystem == null)
        {
            composer.Api.Logger.Warning("[StorageTweaks] StorageTweaksModSystem not found for gui dialog inventory");
            return;
        }

        modSystem.InventoryActionButtons!.ComposeGui(composer);
    }

    [HarmonyPatch(typeof(GuiDialog), "OnGuiClosed")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void OnGuiClosed(GuiDialog __instance)
    {
        if (__instance is not GuiDialogInventory && __instance.GetType().Name != "GuiDialogSurvivalInventory") return;
        var capi = GetApi(__instance);
        var modSystem = capi.ModLoader.GetModSystem<StorageTweaksModSystem>();
        if (modSystem?.FavoritesManager == null) return;
        modSystem.FavoritesManager.IsFavoriteModeActive = false;
    }

    private static ICoreClientAPI GetApi(GuiDialog dialog)
    {
        var field = dialog.GetType().GetField("capi", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (ICoreClientAPI)field.GetValue(dialog)!;
    }
}
