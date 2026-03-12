using Verse;

using System.Collections.Generic;

namespace Despicable;

/// <summary>
/// Thin compatibility bridge so legacy call sites can adopt the shared manual-menu builder/host incrementally.
/// </summary>
public static class ManualMenuLegacyAdapter
{
    public static List<FloatMenuOption> BuildOptions(IEnumerable<ManualMenuOptionSpec> specs)
    {
        return ManualMenuBuilder.BuildOptions(specs);
    }

    public static FloatMenu Open(IEnumerable<ManualMenuOptionSpec> specs, string title = null, bool givesColonistOrders = false, bool vanishIfMouseDistant = false)
    {
        var request = new ManualMenuRequest(title, specs)
        {
            GivesColonistOrders = givesColonistOrders,
            VanishIfMouseDistant = vanishIfMouseDistant
        };

        return ManualMenuHost.Open(request);
    }
}
