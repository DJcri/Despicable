using System;
// Guardrail-Reason: Manual menu option building stays centralized because reflection-based constructor selection and member backfill share one version-resilient seam.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Despicable;

/// <summary>
/// Converts ManualMenuOptionSpec entries into vanilla FloatMenuOption instances.
/// Reflection keeps this additive and resilient across constructor-shape drift without forcing callers to care about every overload.
/// </summary>
public static class ManualMenuBuilder
{
    private static readonly ConstructorInfo[] floatMenuOptionConstructors = typeof(FloatMenuOption)
        .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly MethodInfo checkboxLabeledFactory = typeof(FloatMenuOption)
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .FirstOrDefault(m => m.Name == "CheckboxLabeled" && typeof(FloatMenuOption).IsAssignableFrom(m.ReturnType));

    private static readonly MemberInfo disabledMember = ManualMenuMemberUtil.ResolveMember(typeof(FloatMenuOption), "Disabled", "disabled");
    private static readonly MemberInfo tooltipMember = ManualMenuMemberUtil.ResolveMember(typeof(FloatMenuOption), "tooltip", "Tooltip");
    private static readonly MemberInfo shownItemMember = ManualMenuMemberUtil.ResolveMember(typeof(FloatMenuOption), "shownItem", "ShownItemForIcon");
    private static readonly MemberInfo iconThingMember = ManualMenuMemberUtil.ResolveMember(typeof(FloatMenuOption), "iconThing", "IconThing");
    private static readonly MemberInfo iconTexMember = ManualMenuMemberUtil.ResolveMember(typeof(FloatMenuOption), "iconTex", "IconTex");
    private static readonly MemberInfo iconColorMember = ManualMenuMemberUtil.ResolveMember(typeof(FloatMenuOption), "iconColor", "IconColor");
    private static readonly MemberInfo iconJustificationMember = ManualMenuMemberUtil.ResolveMember(typeof(FloatMenuOption), "iconJustification", "IconJustification", "horizontalJustification", "HorizontalJustification");

    public static List<FloatMenuOption> BuildOptions(IEnumerable<ManualMenuOptionSpec> specs)
    {
        var options = new List<FloatMenuOption>();
        if (specs == null)
            return options;

        foreach (ManualMenuOptionSpec spec in specs)
        {
            FloatMenuOption option = BuildOption(spec);
            if (option != null)
                options.Add(option);
        }

        return options;
    }

    public static FloatMenuOption BuildOption(ManualMenuOptionSpec spec)
    {
        if (spec == null)
            return null;

        var local = spec;
        FloatMenuOption option = TryBuildCheckboxOption(local)
            ?? TryBuildReflectedOption(local)
            ?? new FloatMenuOption(local.Label ?? string.Empty, WrapAction(local.Action), local.Priority);

        ApplyPostBuildState(option, local);
        return option;
    }

    private static FloatMenuOption TryBuildCheckboxOption(ManualMenuOptionSpec spec)
    {
        if (!spec.CheckboxOn.HasValue || checkboxLabeledFactory == null)
            return null;

        Dictionary<string, object> desired = BuildDesiredValueMap(spec, useCheckboxFactory: true);
        if (!TryCreateFromMethod(checkboxLabeledFactory, desired, out object result))
            return null;

        return result as FloatMenuOption;
    }

    private static FloatMenuOption TryBuildReflectedOption(ManualMenuOptionSpec spec)
    {
        Dictionary<string, object> desired = BuildDesiredValueMap(spec, useCheckboxFactory: false);

        ConstructorInfo bestCtor = null;
        object[] bestArgs = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < floatMenuOptionConstructors.Length; i++)
        {
            ConstructorInfo ctor = floatMenuOptionConstructors[i];
            if (!TryBuildArgumentArray(ctor.GetParameters(), desired, out object[] args, out int score))
                continue;

            if (score > bestScore)
            {
                bestCtor = ctor;
                bestArgs = args;
                bestScore = score;
            }
        }

        if (bestCtor == null)
            return null;

        try
        {
            return bestCtor.Invoke(bestArgs) as FloatMenuOption;
        }
        catch (Exception ex)
        {
            Log.Warning($"[Despicable2.ManualMenu] Failed to build reflected FloatMenuOption '{spec.Label}': {ex.Message}");
            return null;
        }
    }

    private static Dictionary<string, object> BuildDesiredValueMap(ManualMenuOptionSpec spec, bool useCheckboxFactory)
    {
        string tooltip = ResolveTooltip(spec);

        var desired = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = spec.Label ?? string.Empty,
            ["text"] = spec.Label ?? string.Empty,
            ["action"] = WrapAction(spec.Action),
            ["priority"] = spec.Priority,
            ["mouseoverGuiAction"] = spec.MouseoverGuiAction,
            ["mouseoverAction"] = spec.MouseoverGuiAction,
            ["revalidateClickTarget"] = spec.RevalidateClickTarget,
            ["revalidateWorldClickTarget"] = spec.RevalidateWorldClickTarget,
            ["extraPartWidth"] = spec.ExtraPartWidth,
            ["extraPartOnGUI"] = spec.ExtraPartOnGUI,
            ["shownItemForIcon"] = spec.ShownItemForIcon,
            ["shownItem"] = spec.ShownItemForIcon,
            ["iconThing"] = spec.IconThing,
            ["thingIcon"] = spec.IconThing,
            ["iconTex"] = spec.IconTex,
            ["itemIcon"] = spec.IconTex,
            ["iconColor"] = ResolveIconColor(spec),
            ["color"] = ResolveIconColor(spec),
            ["iconJustification"] = spec.IconJustification,
            ["horizontalJustification"] = spec.IconJustification,
            ["tooltip"] = tooltip,
            ["checkboxOn"] = spec.CheckboxOn,
            ["checkOn"] = spec.CheckboxOn,
            ["checkboxState"] = spec.CheckboxOn,
            ["toggleAction"] = spec.ToggleAction,
            ["checkboxClickedAction"] = spec.ToggleAction
        };

        if (useCheckboxFactory)
        {
            desired["action"] = WrapAction(spec.ToggleAction ?? spec.Action);
        }

        return desired;
    }

    private static Color? ResolveIconColor(ManualMenuOptionSpec spec)
    {
        if (spec == null)
            return null;

        if (spec.IconColor.HasValue)
            return spec.IconColor.Value;

        // When using texture/thing/def icons, default to opaque white so the icon is visible.
        if (spec.IconTex != null || spec.IconThing != null || spec.ShownItemForIcon != null)
            return Color.white;

        return null;
    }


    private static bool TryCreateFromMethod(MethodInfo method, Dictionary<string, object> desired, out object result)
    {
        result = null;
        if (method == null)
            return false;

        if (!TryBuildArgumentArray(method.GetParameters(), desired, out object[] args, out _))
            return false;

        try
        {
            result = method.Invoke(null, args);
            return result != null;
        }
        catch (Exception ex)
        {
            Log.Warning($"[Despicable2.ManualMenu] Failed to build checkbox FloatMenuOption: {ex.Message}");
            return false;
        }
    }

    private static bool TryBuildArgumentArray(ParameterInfo[] parameters, Dictionary<string, object> desired, out object[] args, out int score)
    {
        args = new object[parameters.Length];
        score = 0;

        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];

            if (TryResolveParameterValue(parameter, desired, out object value, out bool matchedDesired))
            {
                args[i] = value;
                score += matchedDesired ? 10 : 1;
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                args[i] = parameter.DefaultValue;
                score -= 1;
                continue;
            }

            if (TryCreateNullLike(parameter.ParameterType, out object nullLike))
            {
                args[i] = nullLike;
                score -= 2;
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool TryResolveParameterValue(ParameterInfo parameter, Dictionary<string, object> desired, out object value, out bool matchedDesired)
    {
        matchedDesired = false;

        if (parameter == null)
        {
            value = null;
            return false;
        }

        string name = parameter.Name ?? string.Empty;
        if (desired.TryGetValue(name, out object directValue) && TryConvertValue(directValue, parameter.ParameterType, out value))
        {
            matchedDesired = true;
            return true;
        }

        if (IsActionRectType(parameter.ParameterType) && desired.TryGetValue("extraPartOnGUI", out object extraPart) && TryConvertValue(extraPart, parameter.ParameterType, out value))
        {
            matchedDesired = true;
            return true;
        }

        if (parameter.Position == 1 && parameter.ParameterType == typeof(Action)
            && desired.TryGetValue("action", out object action)
            && TryConvertValue(action, parameter.ParameterType, out value))
        {
            matchedDesired = true;
            return true;
        }

        if (parameter.Position == 0 && parameter.ParameterType == typeof(string)
            && desired.TryGetValue("label", out object label)
            && TryConvertValue(label, parameter.ParameterType, out value))
        {
            matchedDesired = true;
            return true;
        }

        if (parameter.Position == 2 && parameter.ParameterType == typeof(MenuOptionPriority)
            && desired.TryGetValue("priority", out object priority)
            && TryConvertValue(priority, parameter.ParameterType, out value))
        {
            matchedDesired = true;
            return true;
        }

        if (parameter.ParameterType == typeof(Thing) && TryResolveFirstAssignable(desired, parameter.ParameterType, out value, "revalidateClickTarget", "iconThing"))
        {
            matchedDesired = true;
            return true;
        }

        if (parameter.ParameterType == typeof(ThingDef) && TryResolveFirstAssignable(desired, parameter.ParameterType, out value, "shownItemForIcon"))
        {
            matchedDesired = true;
            return true;
        }

        if (parameter.ParameterType == typeof(Texture2D) && TryResolveFirstAssignable(desired, parameter.ParameterType, out value, "iconTex", "itemIcon"))
        {
            matchedDesired = true;
            return true;
        }

        if (parameter.ParameterType == typeof(WorldObject) && TryResolveFirstAssignable(desired, parameter.ParameterType, out value, "revalidateWorldClickTarget"))
        {
            matchedDesired = true;
            return true;
        }

        if (parameter.ParameterType == typeof(HorizontalJustification) && desired.TryGetValue("horizontalJustification", out object justification) && TryConvertValue(justification, parameter.ParameterType, out value))
        {
            matchedDesired = true;
            return true;
        }

        if ((parameter.ParameterType == typeof(Color?) || parameter.ParameterType == typeof(Color))
            && desired.TryGetValue("color", out object color)
            && TryConvertValue(color, parameter.ParameterType, out value))
        {
            matchedDesired = true;
            return true;
        }

        if ((parameter.ParameterType == typeof(bool?) || parameter.ParameterType == typeof(bool))
            && desired.TryGetValue("checkboxState", out object checkboxState)
            && TryConvertValue(checkboxState, parameter.ParameterType, out value))
        {
            matchedDesired = true;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryResolveFirstAssignable(Dictionary<string, object> desired, Type targetType, out object value, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (!desired.TryGetValue(keys[i], out object candidate))
                continue;

            if (TryConvertValue(candidate, targetType, out value))
                return true;
        }

        value = null;
        return false;
    }

    private static bool TryConvertValue(object input, Type targetType, out object value)
    {
        if (targetType == null)
        {
            value = null;
            return false;
        }

        if (input == null)
            return TryCreateNullLike(targetType, out value);

        Type nonNullableTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Special-case conversions for RimWorld/Unity types that Convert.ChangeType can't handle.
        if (nonNullableTarget == typeof(TipSignal))
        {
            if (input is TipSignal tip)
            {
                value = tip;
                return true;
            }

            // TipSignal has constructors that accept string/TaggedString; fall back to ToString().
            value = new TipSignal(input.ToString());
            return true;
        }

        if (nonNullableTarget == typeof(Color))
        {
            if (input is Color c)
            {
                value = c;
                return true;
            }

            value = default(Color);
            return true;
        }


        if (nonNullableTarget.IsInstanceOfType(input))
        {
            value = input;
            return true;
        }

        try
        {
            if (nonNullableTarget.IsEnum)
            {
                if (input is string enumText)
                {
                    value = Enum.Parse(nonNullableTarget, enumText, ignoreCase: true);
                    return true;
                }

                value = Enum.ToObject(nonNullableTarget, input);
                return true;
            }

            if (nonNullableTarget == typeof(string))
            {
                value = input.ToString();
                return true;
            }

            value = Convert.ChangeType(input, nonNullableTarget);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static bool TryCreateNullLike(Type targetType, out object value)
    {
        Type nonNullableTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (nonNullableTarget == typeof(TipSignal))
        {
            value = default(TipSignal);
            return true;
        }

        if (nonNullableTarget == typeof(Color))
        {
            value = default(Color);
            return true;
        }

        if (!nonNullableTarget.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
        {
            value = null;
            return true;
        }

        value = Activator.CreateInstance(nonNullableTarget);
        return true;
    }

    private static bool IsActionRectType(Type type)
    {
        if (!type.IsGenericType)
            return false;

        Type generic = type.GetGenericTypeDefinition();
        if (generic != typeof(Action<>))
            return false;

        Type[] args = type.GetGenericArguments();
        return args.Length == 1 && args[0] == typeof(Rect);
    }

    private static void ApplyPostBuildState(FloatMenuOption option, ManualMenuOptionSpec spec)
    {
        if (option == null || spec == null)
            return;

        if (spec.ShownItemForIcon != null)
            ManualMenuMemberUtil.TrySetMemberValue(shownItemMember, option, spec.ShownItemForIcon, "set member", TryConvertValue);

        if (spec.IconThing != null)
            ManualMenuMemberUtil.TrySetMemberValue(iconThingMember, option, spec.IconThing, "set member", TryConvertValue);

        if (spec.IconTex != null)
            ManualMenuMemberUtil.TrySetMemberValue(iconTexMember, option, spec.IconTex, "set member", TryConvertValue);

        Color? iconColor = ResolveIconColor(spec);
        if (iconColor.HasValue)
            ManualMenuMemberUtil.TrySetMemberValue(iconColorMember, option, iconColor.Value, "set member", TryConvertValue);

        ManualMenuMemberUtil.TrySetMemberValue(iconJustificationMember, option, spec.IconJustification, "set member", TryConvertValue);

        string tooltip = ResolveTooltip(spec);
        if (!tooltip.NullOrEmpty())
            ManualMenuMemberUtil.TrySetMemberValue(tooltipMember, option, tooltip, "set member", TryConvertValue);

        if (spec.IsDisabled)
            ManualMenuMemberUtil.TrySetMemberValue(disabledMember, option, true, "set member", TryConvertValue);
    }

    private static string ResolveTooltip(ManualMenuOptionSpec spec)
    {
        if (spec == null)
            return null;

        return !spec.Tooltip.NullOrEmpty() ? spec.Tooltip : spec.DisabledReason;
    }

    private static Action WrapAction(Action action)
    {
        if (action == null)
            return null;

        return () => action();
    }
}
