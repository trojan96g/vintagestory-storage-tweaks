// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global

using System;
using System.Linq;
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

        if (composer.DialogName != "inventory-backpack")
        {
            return;
        }

        capi.Logger.Debug("[StorageTweaks] Composing inventory action buttons.");

        var modSystem = capi.ModLoader.GetModSystem<StorageTweaksModSystem>();

        if (modSystem == null)
        {
            composer.Api.Logger.Warning("[StorageTweaks] StorageTweaksModSystem not found for gui dialog inventory");
            return;
        }

        modSystem.InventoryActionButtons!.ComposeGui(composer);
    }

    /// <summary>
    /// A fallback patch in case AddDialogTitleBar doesn't get run. For some reason with the
    /// Improved Handbook Recipe Helper mod my AddDialogTitleBar never gets called.
    /// But as soon as I add a patch for ComposeSurvivalInvDialog it does get run meaning I would be adding the buttons
    /// twice accept that I have a check to prevent it. I don't understand all this patching stuff, but I'll keep this
    /// one as a fallback just in case AddDialogTitleBar doesn't get run. However, this postfix patch will never run
    /// with the Backpacks mod since it uses GuiDialogSurvivalInventory from the playerinventorylib mod. As far as I know
    /// the handbook recipe helper mod does not patch anything in playerinventorylib meaning AddDialogTitleBar patch
    /// is uneffected and should work just fine with playerinventorylib/Backpacks mod.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(typeof(GuiDialogInventory), "ComposeSurvivalInvDialog")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void ComposeSurvivalInvDialog(GuiDialogInventory __instance)
    {
        var capi = GetApi(__instance);
        capi.Logger.Debug("[StorageTweaks] ComposeSurvivalInvDialog: {0}", __instance.GetType().Name);
        var composer = GetGuiComposer(__instance);
        if (composer == null)
        {
            capi.Logger.Warning("[StorageTweaks] Failed to find GuiComposer in ComposeSurvivalInvDialog");
            return;
        }

        var storageTweaksKeys = new[]
        {
            "storagetweaks-sort", "storagetweaks-store-nearby", "storagetweaks-favorite",
            "storagetweaks-stack-perishables"
        };
        if (storageTweaksKeys.Any(key => composer[key] != null))
        {
            return;
        }

        var modSystem = capi.ModLoader.GetModSystem<StorageTweaksModSystem>();
        if (modSystem == null)
        {
            capi.Logger.Warning("[StorageTweaks] Failed to get StorageTweaksModSystem in ComposeSurvivalInvDialog");
            return;
        }

        modSystem.InventoryActionButtons!.ComposeGui(composer);
    }

    [HarmonyPatch(typeof(GuiDialog), "OnGuiClosed")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void OnGuiClosed(GuiDialog __instance)
    {
        if (__instance is not GuiDialogInventory && __instance.GetType().Name != "GuiDialogSurvivalInventory")
        {
            return;
        }

        var capi = GetApi(__instance);
        var modSystem = capi.ModLoader.GetModSystem<StorageTweaksModSystem>();
        if (modSystem?.FavoritesManager == null)
        {
            return;
        }

        modSystem.FavoritesManager.IsFavoriteModeActive = false;
    }

    public static ICoreClientAPI GetApi(GuiDialog dialog)
    {
        var field = dialog.GetType().GetField("capi", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (ICoreClientAPI)field.GetValue(dialog)!;
    }

    private static GuiComposer? GetGuiComposer(GuiDialogInventory dialog)
    {
        var field = dialog.GetType().GetField("survivalInvDialog", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (GuiComposer?)field.GetValue(dialog);
    }

    public static void Reload(ICoreClientAPI api)
    {
        var dialog = api.Gui.OpenedGuis.Find(d => d.Composers.Values.Any(c => c.DialogName == "inventory-backpack"));

        if (dialog is not GuiDialogInventory dialogInventory)
        {
            return;
        }

        dialogInventory.ComposeGui(false);
    }
}