using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Despicable.UIFramework.Blueprints;
/// <summary>
/// Builder for consistent FloatMenu option sets (disabled reasons, optional search).
///
/// Notes:
/// - FloatMenu draws in its own context; we don't attempt rect recording for rows.
/// - For searchable menus, we provide a small D2WindowBlueprint-based dialog that filters and invokes options.
///
/// Typical use:
///   D2FloatMenuBlueprint.Open(myOptions, searchable: true, title: "Pick a thing");
/// </summary>
public static class D2FloatMenuBlueprint
{
    public readonly struct Option
    {
        public readonly string Label;
        public readonly Action Action;
        public readonly bool Disabled;
        public readonly string DisabledReason;
        public readonly string Tooltip;

        public Option(string label, Action action, bool disabled = false, string disabledReason = null, string tooltip = null)
        {
            Label = label ?? string.Empty;
            Action = action;
            Disabled = disabled;
            DisabledReason = disabledReason;
            Tooltip = tooltip;
        }
    }

    public static List<FloatMenuOption> BuildOptions(IEnumerable<Option> options)
    {
        var list = new List<FloatMenuOption>();
        if (options == null) return list;

        foreach (var o in options)
        {
            var local = o; // capture
            FloatMenuOption fmo;

            if (local.Disabled)
            {
                fmo = new FloatMenuOption(local.Label, null);
                if (!local.DisabledReason.NullOrEmpty())
                    fmo.tooltip = local.DisabledReason;
                else if (!local.Tooltip.NullOrEmpty())
                    fmo.tooltip = local.Tooltip;
            }
            else
            {
                fmo = new FloatMenuOption(local.Label, () => local.Action?.Invoke());
                if (!local.Tooltip.NullOrEmpty())
                    fmo.tooltip = local.Tooltip;
            }

            list.Add(fmo);
        }

        return list;
    }

    /// <summary>
    /// Opens either a normal FloatMenu (default) or a searchable menu dialog if requested.
    /// </summary>
    public static void Open(IEnumerable<Option> options, bool searchable = false, string title = null, int searchableThreshold = 12)
    {
        if (options == null) return;

        // Materialize once; we may open and filter.
        var list = options as List<Option> ?? options.ToList();

        bool useSearch = searchable && list.Count >= searchableThreshold;

        if (!useSearch)
        {
            Find.WindowStack.Add(new FloatMenu(BuildOptions(list)));
            return;
        }

        Find.WindowStack.Add(new Dialog_D2SearchableFloatMenu(list, title));
    }
}
