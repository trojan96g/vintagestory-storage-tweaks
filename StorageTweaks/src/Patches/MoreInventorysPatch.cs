// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Client;

namespace StorageTweaks.Patches;

/// <summary>
///     Dynamic patch for MoreInventorys mod. Applied if the MoreInventorys mod is present.
///     Using <see cref="AccessTools" /> to patch the mod without directly depending on it.
/// </summary>
public static class MoreInventorysPatch
{
    private const string CrateDialogType = "MoreInventorys.src.GuiFolder.GuiDialogCrateClosed";
    private const string DynamicDialogType = "MoreInventorys.src.GuiFolder.GuiDialogDynamic";

    private const string SetupDialogMethod = "SetupDialog";
    private const string OnInventorySlotModifiedMethod = "OnInventorySlotModified";

    /// <summary>
    ///     Records dialogs with a <c>SetupDialog</c> invocation queued on the main-thread task
    ///     list but not yet executed. Lets <see cref="OnInventorySlotModifiedPrefix" /> skip the
    ///     original "enqueue SetupDialog for every slot change" path for the second and following
    ///     slot-change events in the same flood (e.g. the burst of slot-update packets the server
    ///     flushes after a bulk sort), so MoreInventorys ends up rebuilding its slot grid exactly
    ///     once per flood instead of once per changed slot. Without this, sorting a Rack enqueues
    ///     many <c>SetupDialog</c> calls in a single frame causing many full GUI rebuilds which
    ///     causes a 2-3 second client freeze.
    ///     Keys are held weakly (via <see cref="ConditionalWeakTable{TKey,TValue}" />) so a dialog
    ///     being closed/GCed without ever running the queued SetupDialog (or disposed by
    ///     MoreInventorys from a non-patched code path) can't leak an entry forever.
    /// </summary>
    private static readonly ConditionalWeakTable<GuiDialog, StrongBox<bool>> PendingSetup = new();

    /// <summary>
    ///     Patches the More Inventorys SetupDialog methods if the mod
    ///     is present. Subsequent calls skip already-patched methods.
    /// </summary>
    public static void Apply(Harmony harmony, ICoreClientAPI capi)
    {
        PatchOne(harmony, capi, CrateDialogType);
        PatchOne(harmony, capi, DynamicDialogType);
    }

    private static void PatchOne(Harmony harmony, ICoreClientAPI capi, string typeName)
    {
        var type = AccessTools.TypeByName(typeName);
        if (type == null)
        {
            capi.Logger.Debug("[StorageTweaks] More Inventorys type {0} not loaded; skipping patch",
                typeName);
            return;
        }

        var setupDialog = AccessTools.Method(type, SetupDialogMethod);
        if (setupDialog == null)
        {
            capi.Logger.Warning(
                "[StorageTweaks] More Inventorys {0}.{1} not found; cannot refresh buttons on slot change",
                typeName, SetupDialogMethod);
            return;
        }

        var onSlotModified = AccessTools.Method(type, OnInventorySlotModifiedMethod);
        if (onSlotModified == null)
        {
            capi.Logger.Warning(
                "[StorageTweaks] More Inventorys {0}.{1} not found; cannot debounce GUI rebuilds during bulk updates",
                typeName, OnInventorySlotModifiedMethod);
        }

        var postfix = new HarmonyMethod(typeof(MoreInventorysPatch), nameof(SetupDialogPostfix));
        var prefix = new HarmonyMethod(typeof(MoreInventorysPatch), nameof(SetupDialogPrefix));
        harmony.Patch(setupDialog, prefix, postfix);
        capi.Logger.Debug("[StorageTweaks] Patched {0}.{1} for button refresh + debounced rebuild",
            typeName, SetupDialogMethod);

        if (onSlotModified == null)
        {
            return;
        }

        var debouncePrefix =
            new HarmonyMethod(typeof(MoreInventorysPatch), nameof(OnInventorySlotModifiedPrefix));
        harmony.Patch(onSlotModified, debouncePrefix);
        capi.Logger.Debug("[StorageTweaks] Patched {0}.{1} for debounced GUI rebuild",
            typeName, OnInventorySlotModifiedMethod);
    }

    /// <summary>
    ///     Prefix on MoreInventorys' <c>OnInventorySlotModified</c>. Returns <c>false</c> to skip
    ///     the original <c>EnqueueMainThreadTask(SetupDialog, ...)</c> call when a SetupDialog is
    ///     already pending for this dialog instance - the queued SetupDialog will reflect ALL the
    ///     accumulated slot changes, not just the one(s) that triggered the first enqueue. The
    ///     pending flag is cleared in <see cref="SetupDialogPrefix" /> once the queued rebuild runs.
    /// </summary>
    // ReSharper disable once UnusedParameter.Global
    // ReSharper disable once MemberCanBePrivate.Global
    internal static bool OnInventorySlotModifiedPrefix(GuiDialog __instance)
    {
        // No need to lock: OnInventorySlotModified runs synchronously on the client main thread
        // (it is invoked from InventoryNetworkUtil.UpdateFromPacket during packet processing),
        // and the queued SetupDialog that resets the flag also runs on the main thread later in
        // the same frame. Cross-thread reads/writes cannot happen.
        if (PendingSetup.TryGetValue(__instance, out var box))
        {
            if (box.Value)
            {
                return false;
            }

            box.Value = true;
            return true;
        }

        PendingSetup.Add(__instance, new StrongBox<bool>(true));
        return true;
    }

    /// <summary>
    ///     Prefix on MoreInventorys' <c>SetupDialog</c>. Clears the pending flag established in
    ///     <see cref="OnInventorySlotModifiedPrefix" />, so the next slot-change event for the same
    ///     dialog instance is allowed to enqueue a fresh rebuild. The StrongBox itself is kept in
    ///     the weak table so future slot changes for the same dialog instance reuse the same box.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    internal static void SetupDialogPrefix(GuiDialog __instance)
    {
        // SetupDialog is also called from constructors/initial open where no enqueue flag
        // was set; the box is simply missing in that path and ConditionalWeakTable.TryGetValue
        // is safe on a missing key, so there is nothing to clear there.
        if (PendingSetup.TryGetValue(__instance, out var box))
        {
            box.Value = false;
        }
    }

    // ReSharper disable once InconsistentNaming
    private static void SetupDialogPostfix(GuiDialog __instance)
    {
        GuiDialogBlockEntityInventoryPatch.OnGuiDialogOpened(__instance);
    }
}
