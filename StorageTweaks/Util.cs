using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace StorageTweaks;

public static class Util
{
    public static bool IsPlayerBackpack(IInventory inventory)
    {
        // vs 1.21 used class name InventoryPlayerBackPacks and vs 1.22+ use class name InventoryPlayerBackpacks
        // the difference is in the casing of Backpacks
        return inventory.ClassName == GlobalConstants.backpackInvClassName;
    }

    public static bool IsPlayerHotbar(IInventory inventory)
    {
        return inventory.ClassName == GlobalConstants.hotBarInvClassName;
    }

    public static List<ItemSlot> GetInventorySlots(IInventory inventory)
    {
        var slots = inventory.ToList();
        if (!IsPlayerBackpack(inventory)) return slots;
        var ok = TryGetField(inventory, "CountForNetworkPacket", out int countForNetworkPacket);
        if (!ok)
        {
            StorageTweaksModSystem.Logger().Warning("Failed to get CountForNetworkPacket from inventory");
        }

        return slots.Slice(countForNetworkPacket, slots.Count - countForNetworkPacket);
    }

    private static bool TryGetField<T>(object? instance, string fieldName, out T? value,
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance)
    {
        value = default;
        var fieldInfo = instance?.GetType().GetField(fieldName, bindingFlags);
        if (fieldInfo == null || !typeof(T).IsAssignableFrom(fieldInfo.FieldType)) return false;
        value = (T)fieldInfo.GetValue(instance)!;
        return true;
    }
}