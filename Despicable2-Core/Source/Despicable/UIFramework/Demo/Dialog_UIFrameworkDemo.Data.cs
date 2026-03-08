using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
using Despicable.UIFramework;
using Despicable.UIFramework.Blueprints;
using Despicable.UIFramework.Layout;
using Despicable.UIFramework.Controls;
using Despicable;

namespace Despicable.UIFramework.Demo;
public sealed partial class Dialog_UIFrameworkDemo
{
    private List<FloatMenuOption> BuildScenarioOptions()
    {
        var opts = new List<FloatMenuOption>();
        opts.Add(new FloatMenuOption("Small", () => SetScenario(DemoScenario.Small)));
        opts.Add(new FloatMenuOption("Medium", () => SetScenario(DemoScenario.Medium)));
        opts.Add(new FloatMenuOption("Huge", () => SetScenario(DemoScenario.Huge)));
        opts.Add(new FloatMenuOption("LongText", () => SetScenario(DemoScenario.LongText)));
        opts.Add(new FloatMenuOption("Iconless", () => SetScenario(DemoScenario.Iconless)));
        opts.Add(new FloatMenuOption("Mixed", () => SetScenario(DemoScenario.Mixed)));
        return opts;
    }

    private List<FloatMenuOption> BuildModeOptions()
    {
        var opts = new List<FloatMenuOption>();
        opts.Add(new FloatMenuOption("A", () => _mode = DemoMode.A));
        opts.Add(new FloatMenuOption("B", () => _mode = DemoMode.B));
        opts.Add(new FloatMenuOption("C", () => _mode = DemoMode.C));
        return opts;
    }

    private List<D2FloatMenuBlueprint.Option> BuildSearchMenuOptions()
    {
        // Intentionally > threshold so the searchable dialog is exercised.
        var opts = new List<D2FloatMenuBlueprint.Option>(32);

        for (int i = 0; i < 24; i++)
        {
            int local = i;
            bool disabled = (local % 9 == 0);
            string label = "Option " + local.ToString("00") + (disabled ? " (disabled)" : string.Empty);
            string tooltip = (local % 5 == 0) ? "Tooltip for option " + local.ToString("00") : null;

            if (disabled)
            {
                opts.Add(new D2FloatMenuBlueprint.Option(
                    label,
                    action: null,
                    disabled: true,
                    disabledReason: "Disabled for demo reasons.",
                    tooltip: tooltip));
            }
            else
            {
                opts.Add(new D2FloatMenuBlueprint.Option(
                    label,
                    action: () => Messages.Message("Picked: " + local.ToString("00"), MessageTypeDefOf.TaskCompletion, false),
                    disabled: false,
                    disabledReason: null,
                    tooltip: tooltip));
            }
        }

        // A couple of extra long labels to validate clipping and filtering.
        opts.Add(new D2FloatMenuBlueprint.Option(
            "A very long option label that should clip nicely in the list and still filter",
            action: () => Messages.Message("Picked: long label", MessageTypeDefOf.NeutralEvent, false),
            tooltip: "Long label option"));
        opts.Add(new D2FloatMenuBlueprint.Option(
            "Zebra option",
            action: () => Messages.Message("Picked: zebra", MessageTypeDefOf.NeutralEvent, false),
            tooltip: "Try searching for 'zeb'"));

        return opts;
    }

    private void SetScenario(DemoScenario scenario)
    {
        if (_scenario == scenario) return;
        _scenario = scenario;
        _selectedIndex = -1;
        RebuildItems();
    }

    private void RebuildItems()
    {
        _items.Clear();

        int count;
        switch (_scenario)
        {
            case DemoScenario.Small: count = 12; break;
            case DemoScenario.Medium: count = 60; break;
            case DemoScenario.Huge: count = 220; break;
            case DemoScenario.LongText: count = 60; break;
            case DemoScenario.Iconless: count = 60; break;
            default: count = 80; break;
        }

        for (int i = 0; i < count; i++)
        {
            bool longText = _scenario == DemoScenario.LongText || (_scenario == DemoScenario.Mixed && i % 7 == 0);
            bool icon = _scenario != DemoScenario.Iconless && (_scenario != DemoScenario.Mixed ? true : (i % 3 != 0));
            bool disabled = (_scenario == DemoScenario.Mixed && i % 11 == 0);

            string label = longText
                ? "Item " + i.ToString("000") + ": " + LongTextSample
                : "Item " + i.ToString("000");

            string desc = longText
                ? ("This is a long description used to force wrapping. " + LongTextSample)
                : "A short description.";

            _items.Add(new DemoItem
            {
                Label = label,
                Description = desc,
                HasIcon = icon,
                Disabled = disabled
            });
        }
    }

    private List<DemoItem> GetFilteredItems()
    {
        _filtered.Clear();

        string q = _filter ?? string.Empty;
        if (q.Length == 0)
        {
            _filtered.AddRange(_items);
            return _filtered;
        }

        q = q.ToLowerInvariant();
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            if ((it.Label ?? string.Empty).ToLowerInvariant().Contains(q))
                _filtered.Add(it);
        }
        return _filtered;
    }

    private DemoItem GetSelectedOrDefault()
    {
        var list = GetFilteredItems();
        if (_selectedIndex >= 0 && _selectedIndex < list.Count)
            return list[_selectedIndex];

        if (list.Count > 0) return list[0];

        return new DemoItem
        {
            Label = "(no items)",
            Description = "No items in the current scenario.",
            HasIcon = false,
            Disabled = false
        };
    }

    private List<string> GetBullets()
    {
        var b = new List<string>(3);
        b.Add("Short bullet.");
        b.Add("Medium bullet with a bit more text.");

        if (_scenario == DemoScenario.LongText || _scenario == DemoScenario.Huge)
            b.Add("This bullet is intentionally long so it wraps across multiple lines and proves that measurement-driven allocation prevents overlaps and clipping.");
        else
            b.Add("Long bullet (wrap test): the quick brown fox jumps over the lazy dog, repeatedly, until the UI gives up.");

        return b;
    }

    private const string LongTextSample =
        "This is a deliberately long string to stress wrapping and measurement. " +
        "If anything overlaps, clips, or becomes unreadable, validation should complain loudly.";
}
