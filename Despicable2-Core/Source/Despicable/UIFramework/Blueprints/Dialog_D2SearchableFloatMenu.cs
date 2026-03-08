using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.UIFramework;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Blueprints;
/// <summary>
/// Searchable option picker using the D2 window + list helpers.
/// Intended to replace very large FloatMenus that become hard to scan.
/// </summary>
public sealed class Dialog_D2SearchableFloatMenu : D2WindowBlueprint
{
    private readonly List<D2FloatMenuBlueprint.Option> _options;
    private readonly string _title;

    private string _search = string.Empty;
    private Vector2 _scroll = Vector2.zero;
    private int _selectedIndex = -1;

    protected override bool UseBodyScroll => false;
    public override Vector2 InitialSize => new Vector2(520f, 640f);

    public Dialog_D2SearchableFloatMenu(List<D2FloatMenuBlueprint.Option> options, string title = null)
    {
        _options = options ?? new List<D2FloatMenuBlueprint.Option>();
        _title = title ?? "Select";

        doCloseX = true;
        closeOnAccept = false;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = false;
    }

    protected override void DrawHeader(Rect rect)
    {
        var ctx = Ctx;

            var v = new Despicable.UIFramework.Layout.VStack(ctx, rect);
            v.NextSpace(2f);

        // Title
        var titleRow = v.Next(ctx.Style.ControlHeight);
        D2Widgets.LabelClipped(ctx, titleRow, _title, "Header/Title");

        // Search
        var searchRow = v.Next(ctx.Style.ControlHeight);
        D2Fields.SearchBox(ctx, searchRow, ref _search, "Search...");
    }

    protected override void DrawBody(Rect rect)
    {
        var ctx = Ctx;

        var filtered = Filtered();

            const float rowH = 26f;
            D2ListView.Draw(
                ctx,
                rect,
                ref _scroll,
                filtered,
                ref _selectedIndex,
                DrawRow,
                rowHeightOverride: rowH,
                zebra: true,
                label: "SearchableFloatMenu/List");
    }

    private List<D2FloatMenuBlueprint.Option> Filtered()
    {
        if (_search.NullOrEmpty())
            return _options;

        string s = _search.Trim().ToLowerInvariant();
        if (s.Length == 0) return _options;

        return _options.Where(o => (o.Label ?? string.Empty).ToLowerInvariant().Contains(s)).ToList();
    }

    private void DrawRow(UIContext ctx, Rect rowRect, D2FloatMenuBlueprint.Option item, int index, bool selected)
    {
        ctx.RecordRect(rowRect, UIRectTag.Control_MenuRow, item.Label);

        var r = rowRect.ContractedBy(6f);

        bool disabled = item.Disabled;
        string label = item.Label ?? string.Empty;

        // Tooltips: prefer disabled reason.
        if (ctx.Pass == UIPass.Draw)
        {
            if (disabled && !item.DisabledReason.NullOrEmpty())
                TooltipHandler.TipRegion(rowRect, item.DisabledReason);
            else if (!item.Tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(rowRect, item.Tooltip);
        }

        using (new GUIEnabledScope(!disabled))
        using (new TextStateScope(GameFont.Small, TextAnchor.MiddleLeft, false))
        {
            Widgets.Label(r, label);

            if (Widgets.ButtonInvisible(rowRect))
            {
                if (!disabled)
                {
                    item.Action?.Invoke();
                    Close();
                }
            }
        }
    }
}
