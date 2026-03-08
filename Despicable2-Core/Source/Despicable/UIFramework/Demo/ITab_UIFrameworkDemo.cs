using System.Collections.Generic;
using UnityEngine;
using Verse;
using Despicable.UIFramework.Blueprints;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;
using RimWorld;

namespace Despicable.UIFramework.Demo;
/// <summary>
/// A real RimWorld ITab that uses the UI framework.
/// This exists so the demo covers the "inspect tab" surface (tight, scroll-heavy, translation-prone).
///
/// Spawn a demo thing via the debug action: "Spawn UI Framework Demo Tab Thing".
/// Select it, then click the "UI Tab" inspector tab.
/// </summary>
public sealed class ITab_UIFrameworkDemo : ITab
{
    private readonly UIRectRegistry _registry = new();

    private Vector2 _scroll;
    private float _contentHeight;

    private bool _enabled = true;
    private float _intensity = 42f;
    private string _filter = string.Empty;

    private int _count = 12;
    private float _threshold = 0.25f;

    private Vector2 _listScroll;
    private int _selectedIndex = 0;
    private readonly List<string> _items = BuildItems();

    private enum DemoMode { Alpha, Beta, Gamma }
    private DemoMode _mode = DemoMode.Alpha;

    private static List<string> BuildItems()
    {
        var list = new List<string>(64);
        for (int i = 1; i <= 50; i++)
            list.Add("Demo item " + i);
        list.Add("Banana diplomacy");
        list.Add("Karma ledger");
        list.Add("Hero overlay");
        list.Add("Scroll gremlins");
        return list;
    }

    public ITab_UIFrameworkDemo()
    {
        // Keep it compact; this is an inspect tab.
        size = new Vector2(420f, 520f);
        labelKey = "UI Tab";
    }

    protected override void FillTab()
    {
        // RimWorld draws ITabs in a local 0,0 coordinate space.
        Rect full = new(0f, 0f, size.x, size.y);

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            _registry.ValidationMode = UIValidationMode.Off;
            _registry.BeginFrame(GetType().Name, full);

            var ctx = new UIContext(D2UIStyle.Default, _registry, GetType().Name, UIPass.Draw);

            // Pad slightly; inspect tabs are already bordered, so keep it tight.
            Rect inner = full.ContractedBy(10f);

            // Header/body/footer split.
            float headerH = ctx.Style.RowHeight;
            float footerH = ctx.Style.RowHeight;
            float gap = ctx.Style.Gap;
            var bp = new D2ITabBlueprint(ctx, inner, headerH, footerH, gap);

            using (ctx.PushScope("Header"))
                DrawHeader(ctx, bp.Header);

            using (ctx.PushScope("Body"))
                bp.DrawBodyScroll(ctx, ref _scroll, ref _contentHeight, DrawBodyContent, label: "ITab/Scroll");

            using (ctx.PushScope("Footer"))
                DrawFooter(ctx, bp.Footer);

            _registry.Validate(ctx.Style);
        }
    }

    private void DrawHeader(UIContext ctx, Rect rect)
    {
        var h = new HRow(ctx, rect);
        D2Widgets.LabelClipped(ctx, h.NextFixed(rect.width * 0.60f), "UI Framework ITab", "ITab/HeaderTitle");

        // Small status chip.
        Rect chip = h.Remaining();
        ctx.Record(chip, UIRectTag.PanelSoft, "ITab/HeaderChip");
        if (ctx.Pass == UIPass.Measure) return;
        Widgets.DrawBoxSolid(chip, new Color(0f, 0f, 0f, 0.10f));
        using (new TextStateScope(GameFont.Small, TextAnchor.MiddleCenter, true))
            Widgets.Label(chip, _enabled ? "Enabled" : "Disabled");
    }

    private void DrawFooter(UIContext ctx, Rect rect)
    {
        // Footer is intentionally boring: one button that does something visible.
        var h = new HRow(ctx, rect);
        Rect left = h.NextFixed(140f);
        Rect right = h.Remaining();

        if (D2Widgets.ButtonText(ctx, left, "Reset", "ITab/FooterReset"))
        {
            _enabled = true;
            _intensity = 42f;
            _filter = string.Empty;
            _mode = DemoMode.Alpha;
            _count = 12;
            _threshold = 0.25f;
            _selectedIndex = 0;
        }

        D2Widgets.LabelClipped(ctx, right, "(Inspect tab demo)", "ITab/FooterHint");
    }

    private void DrawBodyContent(UIContext ctx, ref VStack v)
    {
        // Controls section
        Rect header = D2LayoutHelpers.NextSectionHeader(ctx, ref v, "ITab/ControlsHeader");
        using (new TextStateScope(GameFont.Medium, TextAnchor.UpperLeft, true))
            D2Widgets.LabelClipped(ctx, header, "Controls", "ITab/ControlsTitle");

        bool en = _enabled;
        D2Widgets.CheckboxLabeled(ctx, v.NextRow(UIRectTag.Input, "ITab/EnabledRow"), "Enabled", ref en, "ITab/Enabled");
        _enabled = en;

        // Slider
        Rect sliderRow = v.NextRow(UIRectTag.Input, "ITab/IntensityRow");
        var hs = new HRow(ctx, sliderRow);
        D2Widgets.Label(ctx, hs.NextFixed(90f), "Intensity", "ITab/IntensityLabel");
        _intensity = D2Widgets.HorizontalSlider(ctx, hs.Remaining(), _intensity, 0f, 100f, showValueLabel: true, label: "ITab/Intensity");

        // Filter field
        Rect filterRow = v.NextRow(UIRectTag.Input, "ITab/FilterRow");
        var hf = new HRow(ctx, filterRow);
        D2Widgets.Label(ctx, hf.NextFixed(90f), "Filter", "ITab/FilterLabel");
        D2Fields.SearchBox(ctx, hf.Remaining(), ref _filter, placeholder: "type to filter...", tooltip: "Filters the demo list below.", label: "ITab/Search");

        // Numeric fields
        Rect numRow = v.NextRow(UIRectTag.Input, "ITab/NumericRow");
        var hn = new HRow(ctx, numRow);
        D2Widgets.Label(ctx, hn.NextFixed(90f), "Count", "ITab/CountLabel");
        Rect countRect = hn.NextFixed(80f);
        D2Fields.IntField(ctx, countRect, ref _count, min: 0, max: 999, tooltip: "An int field with clamping.", label: "ITab/Count");
        D2Widgets.Label(ctx, hn.NextFixed(90f), "Thresh", "ITab/ThreshLabel");
        D2Fields.FloatField(ctx, hn.Remaining(), ref _threshold, min: 0f, max: 1f, tooltip: "A float field with clamping.", label: "ITab/Threshold");

        // Enum dropdown
        Rect modeRow = v.NextRow(UIRectTag.Input, "ITab/ModeRow");
        var hm = new HRow(ctx, modeRow);
        D2Widgets.Label(ctx, hm.NextFixed(90f), "Mode", "ITab/ModeLabel");
        D2Fields.EnumDropdown(ctx, hm.Remaining(), _mode, v2 => _mode = v2, tooltip: "Choose a demo mode.");

        // Searchable menu demo (exercises D2FloatMenuBlueprint + search dialog)
        Rect menuRow = v.NextRow(UIRectTag.Input, "ITab/MenuRow");
        if (D2Widgets.ButtonText(ctx, menuRow, "Open searchable menu", "ITab/OpenSearchMenu"))
        {
            D2FloatMenuBlueprint.Open(BuildMenuOptionsForITab(), searchable: true, title: "ITab Menu", searchableThreshold: 10);
        }

        v.NextDivider(1f, "ITab/Div1");

        // List section (tight surface test: scroll-in-scroll within the ITab body scroll)
        Rect listHeader = D2LayoutHelpers.NextSectionHeader(ctx, ref v, "ITab/ListHeader");
        using (new TextStateScope(GameFont.Medium, TextAnchor.UpperLeft, true))
            D2Widgets.LabelClipped(ctx, listHeader, "List", "ITab/ListTitle");

        // Reserve a fixed height for the list so the outer ITab scroll remains stable.
        float listH = 170f;
        Rect listRect = v.Next(listH, UIRectTag.PanelSoft, "ITab/ListPanel");
        if (ctx.Pass == UIPass.Draw)
            Widgets.DrawBoxSolid(listRect, new Color(0f, 0f, 0f, 0.08f));

        Rect listInner = listRect.ContractedBy(6f);

        // Filter items.
        List<string> view = _items;
        if (!string.IsNullOrEmpty(_filter))
        {
            view = new List<string>();
            string f = _filter.ToLowerInvariant();
            for (int i = 0; i < _items.Count; i++)
            {
                string it = _items[i];
                if (it != null && it.ToLowerInvariant().Contains(f))
                    view.Add(it);
            }
        }

        // Clamp selected index against filtered list.
        if (view.Count == 0) _selectedIndex = -1;
        else if (_selectedIndex >= view.Count) _selectedIndex = 0;

        D2ListView.Draw(ctx, listInner, ref _listScroll, view, ref _selectedIndex,
            (c, row, item, index, selected) =>
            {
                // Simple two-column row: label + small state chip
                var hr = new HRow(c, row);
                D2Widgets.LabelClipped(c, hr.Remaining(), item, "ITab/ListRowLabel");
            },
            rowGap: 0f,
            zebra: true,
            label: "ITab/ListView");

        // Wrapped text section + bullets to exercise the text helpers in a cramped surface.
        Rect infoHeader = D2LayoutHelpers.NextSectionHeader(ctx, ref v, "ITab/InfoHeader");
        using (new TextStateScope(GameFont.Medium, TextAnchor.UpperLeft, true))
            D2Widgets.LabelClipped(ctx, infoHeader, "Info", "ITab/InfoTitle");

        string p = "This is a real ITab driven by the UI framework. It intentionally packs text, fields, and a scroll view into a tight space so layout issues show up fast.";
        v.NextTextBlock(ctx, p, GameFont.Small, padding: 0f, label: "ITab/InfoPara", tag: UIRectTag.Text_Wrapped);

        var bullets = new List<TaggedString>
        {
            "No overlap between rows, even when content wraps.",
            "Scroll height is measured, not guessed.",
            "Rects are recorded for overlay/validation.",
        };
        v.NextBulletList(ctx, bullets, GameFont.Small, padding: 0f, bulletIndent: 18f, bulletGap: 4f, tag: UIRectTag.Text_Bullet, labelPrefix: "ITab/Bullet");
    }

    private static List<D2FloatMenuBlueprint.Option> BuildMenuOptionsForITab()
    {
        var opts = new List<D2FloatMenuBlueprint.Option>(20);

        for (int i = 0; i < 16; i++)
        {
            int local = i;
            bool disabled = (local % 7 == 0);

            if (disabled)
            {
                opts.Add(new D2FloatMenuBlueprint.Option(
                    "ITab Option " + local.ToString("00") + " (disabled)",
                    action: null,
                    disabled: true,
                    disabledReason: "Disabled (demo)",
                    tooltip: "This one is disabled."));
            }
            else
            {
                opts.Add(new D2FloatMenuBlueprint.Option(
                    "ITab Option " + local.ToString("00"),
                    action: () => Messages.Message("Picked ITab option " + local.ToString("00"), MessageTypeDefOf.NeutralEvent, false),
                    disabled: false,
                    disabledReason: null,
                    tooltip: (local % 4 == 0) ? "Has a tooltip." : null));
            }
        }

        opts.Add(new D2FloatMenuBlueprint.Option(
            "A long ITab option label that should still filter",
            action: () => Messages.Message("Picked long ITab option", MessageTypeDefOf.NeutralEvent, false),
            tooltip: "Search for 'long'"));

        return opts;
    }
}
