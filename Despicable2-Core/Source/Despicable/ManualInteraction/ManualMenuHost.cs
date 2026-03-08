using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Despicable;

/// <summary>
/// Shared menu host for ManualInteraction. Keeps menu creation in one place so pages can opt into consistent vanilla behavior.
/// </summary>
public static class ManualMenuHost
{
    private static readonly MemberInfo givesColonistOrdersMember = ResolveMember(typeof(FloatMenu), "givesColonistOrders", "GivesColonistOrders");
    private static readonly MemberInfo vanishIfMouseDistantMember = ResolveMember(typeof(FloatMenu), "vanishIfMouseDistant", "VanishIfMouseDistant");

    public static FloatMenu Open(ManualMenuRequest request)
    {
        if (request == null)
            return null;

        List<FloatMenuOption> options = ManualMenuBuilder.BuildOptions(request.Options);
        if (options.Count == 0)
            return null;

        var menu = new FloatMenu(options);
        TrySetMemberValue(givesColonistOrdersMember, menu, request.GivesColonistOrders);
        TrySetMemberValue(vanishIfMouseDistantMember, menu, request.VanishIfMouseDistant);
        Find.WindowStack.Add(menu);
        return menu;
    }

    private static MemberInfo ResolveMember(System.Type type, params string[] names)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
                return field;

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null)
                return property;
        }

        return null;
    }

    private static void TrySetMemberValue(MemberInfo member, object instance, object value)
    {
        if (member == null || instance == null)
            return;

        try
        {
            switch (member)
            {
                case FieldInfo field:
                    field.SetValue(instance, value);
                    break;
                case PropertyInfo property when property.CanWrite:
                    property.SetValue(instance, value, null);
                    break;
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[Despicable2.ManualMenu] Failed to configure FloatMenu flag '{member.Name}': {ex.Message}");
        }
    }
}
