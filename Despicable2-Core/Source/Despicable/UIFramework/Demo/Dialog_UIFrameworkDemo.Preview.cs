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
    private void DrawPreviewColumn(UIContext ctx, Rect rect)
    {
        using (var gp = ctx.GroupPanel("Preview", rect, soft: false, pad: true))
        {
            var v = ctx.D2VStack(gp.Inner);

            // Decide whether to stack everything or page via tabs.
            // Policy: if the list+details block can't meet its minimums after accounting for the preview card + bullets + gizmos,
            // switch to tabs so the "Browser" can use the full height.
            float pad = ctx.Style.Pad;
            float gap = ctx.Style.Gap;

            // Minimum height for the "Browser" block (List + Details), matching the existing split logic.
            float minListInnerH = (ctx.Style.RowHeight) + ctx.Style.Gap + (ctx.Style.RowHeight * 3f);
            float minListH = minListInnerH + (pad * 2f);
            float minDetailsH = Mathf.Max(200f, (ctx.Style.RowHeight * 3f) + (ctx.Style.ControlHeight * 2f) + (ctx.Style.Gap * 4f) + (pad * 2f));
            float minBrowserH = minListH + gap + minDetailsH;

            // A conservative minimum for the "Preview" block.
            // (We don't need to be perfect here; it's a pressure gauge, not a solver.)
            float minPreviewH = 380f;

            bool useTabs = D2LayoutPolicy.ChooseStackOrTabs(gp.Inner.height, minPreviewH, minBrowserH, gap) == D2LayoutPolicy.StackMode.Tabs;

            if (useTabs)
            {
                // Tab bar
                Rect tabRow = v.NextRow(UIRectTag.Input, "Preview/TabsRow");
                _previewTab = D2Tabs.TabStrip(ctx, tabRow, _previewTab, new[] { "Preview", "Browser" }, "Preview/Tabs");
                v.NextDivider(1f, "Preview/TabsDiv");

                if (_previewTab == 1)
                {
                    DrawBrowserBlock(ctx, ref v, pad);
                    return;
                }
                // else: fall through and draw the Preview block below.
            }

            DemoItem sel = GetSelectedOrDefault();

            // Preview card sized by measured text.
            float contentW = gp.Inner.width - 12f;
            float descH;
            using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
            {
                descH = Mathf.Max(ctx.Style.Line, Text.CalcHeight(sel.Description ?? string.Empty, contentW));
            }

            // Card contains:
            // - Header row
            // - Meter row
            // - Wrapped description
            // - Chips row
            // plus the D2VStack gaps between those elements, plus panel padding.
            float innerH = (ctx.Style.RowHeight * 3f) + descH + (ctx.Style.Gap * 3f);
            // pad already computed above
            float cardH = innerH + (pad * 2f);

            Rect cardSlot = v.Next(cardH, UIRectTag.PanelSoft, "Preview/CardSlot");
            using (var card = ctx.GroupPanel("PreviewCard", cardSlot, soft: true, pad: true))
            {
                var cv = ctx.D2VStack(card.Inner);

                Rect headerRow = cv.NextRow(UIRectTag.None, "Preview/CardHeaderRow");
                var hh = new D2HRow(ctx, headerRow);
                DrawMaybeIcon(ctx, hh.NextFixed(28f), sel.HasIcon ? TexIcons.HeroGizmo : null, "Preview/CardIcon");
                D2Widgets.LabelClipped(ctx, hh.Remaining(), sel.Label ?? "(none)", "Preview/CardTitle");

                Rect meterRow = cv.NextRow(UIRectTag.None, "Preview/MeterRow");
                var hm = new D2HRow(ctx, meterRow);
                D2Widgets.Label(ctx, hm.NextFixed(90f), "Meter", "Preview/MeterLabel");
                D2Widgets.HorizontalSlider(ctx, hm.Remaining(), _intensity, 0f, 100f, showValueLabel: true, label: "Preview/MeterSlider");

                Rect descRect = cv.Next(descH, UIRectTag.Label, "Preview/DescriptionRect");
                D2Widgets.Label(ctx, descRect, sel.Description ?? string.Empty, "Preview/Description");

                Rect chipsRow = cv.NextRow(UIRectTag.None, "Preview/ChipsRow");
                DrawChips(ctx, chipsRow, sel);
            }

            v.NextDivider(1f, "Preview/Div1");

            DrawSectionTitle(ctx, D2LayoutHelpers.NextSectionHeader(ctx, ref v, "Preview/BulletsTitle"), "Bullet points");
            DrawBulletBlock(ctx, ref v, "Preview/Bullets");

            v.NextDivider(1f, "Preview/Div_Gizmos");

            DrawSectionTitle(ctx, D2LayoutHelpers.NextSectionHeader(ctx, ref v, "Preview/GizmosTitle"), "Gizmo grid");
            float gizmoH = Mathf.Max(160f, v.RemainingHeight);
            Rect gizmoSlot = v.Next(gizmoH, UIRectTag.PanelSoft, "Preview/GizmoPanel");
            using (var gzp = ctx.GroupPanel("Gizmos", gizmoSlot, soft: true, pad: true))
            {
                DrawGizmoGrid(ctx, gzp.Inner, "Preview/Gizmos");
            }

            v.NextDivider(1f, "Preview/Div2");

            // If we're not paged by tabs, keep the original stacked "Browser" block at the bottom.
            if (!useTabs)
            {
                DrawBrowserBlock(ctx, ref v, pad);
            }
        }
    }

    private void DrawBrowserBlock(UIContext ctx, ref D2VStack v, float pad)
    {
        // Remaining split: list top, details bottom.
        // Policy:
        // - The list must remain usable (show at least 3 items), since it's our primary navigation.
        // - Details can be internally scrollable, so it may shrink when space is tight.
        Rect remaining = v.Remaining(UIRectTag.Body, "Preview/Remaining");

        // List minimum: title row + gap + 3 visible item rows + panel padding.
        // D2ListView uses a fixed row height, so this is stable.
        float minListInnerH = (ctx.Style.RowHeight) + ctx.Style.Gap + (ctx.Style.RowHeight * 3f);
        float minListH = minListInnerH + (pad * 2f);

        // Details minimum: enough to show the section title + item name + 1 line + a small notes box.
        // If space is tight, Details becomes scrollable so it can shrink below this.
        float minDetailsH = Mathf.Max(200f, (ctx.Style.RowHeight * 3f) + (ctx.Style.ControlHeight * 2f) + (ctx.Style.Gap * 4f) + (pad * 2f));

        D2LayoutHelpers.SplitTopBottomMinBoth(remaining, minListH, minDetailsH, ctx.Style.Gap, out Rect listRect, out Rect detailsRect);

        using (var listPanel = ctx.GroupPanel("List", listRect, soft: true, pad: true))
        {
            var lv = ctx.D2VStack(listPanel.Inner);
            DrawSectionTitle(ctx, lv.NextRow(UIRectTag.Label, "List/Title"), "List");

            Rect listBody = lv.Remaining(UIRectTag.Body, "List/Body");
            var filtered = GetFilteredItems();

            D2ListView.Draw(
                ctx,
                listBody,
                ref _listScroll,
                filtered,
                ref _selectedIndex,
                (c, row, item, idx, selected) => { DrawListRow(c, row, item, idx, selected); },
                rowGap: 0f,
                zebra: true,
                label: "DemoList");
        }

        using (var detailPanel = ctx.GroupPanel("Details", detailsRect, soft: true, pad: true))
        {
            // Details content can exceed its allotted slice depending on window height and scenario.
            // To guarantee "no leaks", make Details internally scrollable.
            D2ScrollView.Draw(
                ctx,
                detailPanel.Inner,
                ref _detailsScroll,
                ref _detailsContentHeight,
                DrawDetailsContent,
                label: "Details/Scroll");
        }
    }

    private void DrawDetailsContent(UIContext ctx, ref D2VStack dv)
    {
        // Section title: allocate a slightly taller header row for Medium font.
        Rect titleRect = D2LayoutHelpers.NextSectionHeader(ctx, ref dv, "Details/Title");
        DrawSectionTitle(ctx, titleRect, "Details");

        DemoItem selected = GetSelectedOrDefault();
        D2Widgets.LabelClipped(ctx, dv.NextRow(UIRectTag.Label, "Details/NameRow"), selected.Label ?? "(none)", "Details/Name");

        string d = selected.Description ?? string.Empty;
        // Wrapped paragraph: allocate + draw in one call (Measure/Draw deterministic).
        dv.NextTextBlock(ctx, d, dv.Bounds.width, GameFont.Small, padding: 0f, label: "Details/Desc", tag: UIRectTag.Text_Wrapped);

        dv.Skip(0f);
        D2Widgets.Label(ctx, dv.NextLine(UIRectTag.Label, "Details/NotesTitle"), "Notes", "Details/NotesTitle");

        // Give Notes a reasonable minimum height; extra content scrolls within this Details view.
        float notesH = Mathf.Max(120f, ctx.Style.ControlHeight * 3f);
        Rect notesRect = dv.Next(notesH, UIRectTag.TextArea, "Details/NotesArea");
        _notes = D2Widgets.TextArea(ctx, notesRect, _notes ?? string.Empty, readOnly: false, label: "Details/Notes");
    }

}
