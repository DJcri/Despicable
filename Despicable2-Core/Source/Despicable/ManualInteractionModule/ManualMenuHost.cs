using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Despicable;

/// <summary>
/// Shared menu host for ManualInteraction. Keeps menu creation in one place so pages can opt into consistent vanilla behavior.
/// </summary>
public static class ManualMenuHost
{
    private static readonly MemberInfo givesColonistOrdersMember = ManualMenuMemberUtil.ResolveMember(typeof(FloatMenu), "givesColonistOrders", "GivesColonistOrders");
    private static readonly MemberInfo vanishIfMouseDistantMember = ManualMenuMemberUtil.ResolveMember(typeof(FloatMenu), "vanishIfMouseDistant", "VanishIfMouseDistant");

    public static FloatMenu Open(ManualMenuRequest request)
    {
        if (request == null)
            return null;

        List<FloatMenuOption> options = ManualMenuBuilder.BuildOptions(request.Options);
        if (options.Count == 0)
            return null;

        var menu = new FloatMenu(options);
        ManualMenuMemberUtil.TrySetMemberValue(givesColonistOrdersMember, menu, request.GivesColonistOrders, "configure FloatMenu flag");
        ManualMenuMemberUtil.TrySetMemberValue(vanishIfMouseDistantMember, menu, request.VanishIfMouseDistant, "configure FloatMenu flag");
        Find.WindowStack.Add(menu);
        return menu;
    }
}
