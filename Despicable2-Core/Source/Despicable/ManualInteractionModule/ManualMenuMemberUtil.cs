using System;
using System.Reflection;

namespace Despicable;

internal delegate bool ManualMenuValueConverter(object value, Type targetType, out object convertedValue);

internal static class ManualMenuMemberUtil
{
    internal static MemberInfo ResolveMember(Type type, params string[] names)
    {
        if (type == null || names == null || names.Length == 0)
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrEmpty(name))
                continue;

            FieldInfo field = type.GetField(name, flags);
            if (field != null)
                return field;

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null)
                return property;
        }

        return null;
    }

    internal static void TrySetMemberValue(MemberInfo member, object instance, object value, string warnContext, ManualMenuValueConverter converter = null)
    {
        if (member == null || instance == null)
            return;

        try
        {
            switch (member)
            {
                case FieldInfo field:
                    object fieldValue = value;
                    if (converter != null && !converter(value, field.FieldType, out fieldValue))
                        return;
                    field.SetValue(instance, fieldValue);
                    break;
                case PropertyInfo property when property.CanWrite:
                    object propertyValue = value;
                    if (converter != null && !converter(value, property.PropertyType, out propertyValue))
                        return;
                    property.SetValue(instance, propertyValue, null);
                    break;
            }
        }
        catch (Exception ex)
        {
            Verse.Log.Warning($"[Despicable2.ManualMenu] Failed to {warnContext} '{member.Name}': {ex.Message}");
        }
    }
}
