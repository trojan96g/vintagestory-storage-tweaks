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

    public static T? TryGetFieldOrProperty<T>(object? instance, string fieldName,
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance)
    {
        var type = instance?.GetType();
        if (type == null)
        {
            return default;
        }

        var fieldInfo = type.GetField(fieldName, bindingFlags);
        if (typeof(T).IsAssignableFrom(fieldInfo?.FieldType))
        {
            return (T)fieldInfo.GetValue(instance)!;
        }

        var propertyInfo = type.GetProperty(fieldName, bindingFlags);
        if (typeof(T).IsAssignableFrom(propertyInfo?.PropertyType))
        {
            return (T)propertyInfo.GetValue(instance)!;
        }

        return default;
    }
}