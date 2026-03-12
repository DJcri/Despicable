using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
// Guardrail-Reason: Hero Karma dialog keeps overview, reputation, and shared draw helpers together while the pre-release UI surface remains one coordinated window.
using UnityEngine;
using Verse;
using Despicable.UIFramework;
using Despicable.UIFramework.Blueprints;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;

namespace Despicable.HeroKarma.UI;

public sealed class Dialog_HeroKarma : D2WindowBlueprint
{
    private enum MainTab
    {
        Overview = 0,
        Reputation = 1
    }

    private static readonly D2UIStyle HeroKarmaStyle = D2UIStyle.Default.With(s =>
    {
        s.HeaderHeight = 58f;
        s.FooterHeight = 0f;
        s.BodyTopPadY = 6f;
        s.BodyBottomPadY = 6f;
        s.RowHeight = 28f;
        s.ButtonHeight = 28f;
    });

    private const float SectionHeaderHeight = 28f;
    private const float SubsectionHeaderHeight = 22f;

    private int _selectedTab;
    private int _repFilterIndex;
    private string _reputationSearch = string.Empty;
    private string _selectedTargetId;
    private bool _selectedIsFaction;

    private Vector2 _overviewScroll;
    private float _overviewContentHeight;
    private Vector2 _repListScroll;
    private float _repListContentHeight;
    private Vector2 _repDetailScroll;
    private float _repDetailContentHeight;

    public Dialog_HeroKarma()
    {
        draggable = true;
        doCloseX = true;
        closeOnClickedOutside = false;
        forcePause = false;
        absorbInputAroundWindow = true;
        resizeable = false;
    }

    public override Vector2 InitialSize => HKUIConstants.WindowSize;

    protected override bool UseBodyScroll => false;
    protected override bool EnableAutoMeasure => false;
    protected override D2UIStyle Style => HeroKarmaStyle;

    public static void ShowWindow()
    {
        if (!HKSettingsUtil.ModuleEnabled)
            return;

        if (GetOpenDialog() != null)
            return;

        Find.WindowStack?.Add(new Dialog_HeroKarma());
    }

    public static Dialog_HeroKarma GetOpenDialog()
    {
        WindowStack ws = Find.WindowStack;
        if (ws?.Windows == null)
            return null;

        for (int i = ws.Windows.Count - 1; i >= 0; i--)
        {
            if (ws.Windows[i] is Dialog_HeroKarma dlg)
                return dlg;
        }

        return null;
    }

    protected override void DrawHeader(Rect rect)
    {
        Pawn hero = HKRuntime.GetHeroPawnSafe();
        var v = Ctx.D2VStack(rect);

        Rect titleRect = v.Next(28f, UIRectTag.Label, "Title");
        Rect subtitleRect = v.Next(24f, UIRectTag.Label, "Subtitle");

        using (new TextStateScope(GameFont.Medium, TextAnchor.UpperLeft, false))
            D2Widgets.LabelClipped(Ctx, titleRect, "D2HK_UI_HeroKarma".Translate(), "Title/Text", "D2HK_UI_HeroKarma".Translate());

        string subtitle = hero != null
            ? "D2HK_UI_HeaderHero".Translate(hero.LabelShortCap).ToString()
            : "D2HK_UI_NoneAssigned".Translate().ToString();
        D2Widgets.LabelClipped(Ctx, subtitleRect, subtitle, "Subtitle/Text", subtitle);
    }

    protected override void DrawBody(Rect rect)
    {
        string[] labels =
        {
            "D2HK_UI_Overview".Translate().ToString(),
            "D2HK_UI_Reputation".Translate().ToString()
        };

        var shell = D2Tabs.VanillaAttachedTabBody(Ctx, rect, ref _selectedTab, labels, "HeroKarmaTabs");
        MainTab tab = (MainTab)Mathf.Clamp(_selectedTab, 0, 1);

        switch (tab)
        {
            case MainTab.Reputation:
                DrawReputationTab(shell.InnerRect);
                break;
            default:
                DrawOverviewTab(shell.InnerRect);
                break;
        }
    }

    private void DrawOverviewTab(Rect rect)
    {
        D2ScrollView.Draw(
            Ctx,
            rect,
            ref _overviewScroll,
            ref _overviewContentHeight,
            delegate(UIContext ctx, ref D2VStack v)
            {
                Pawn hero = HKRuntime.GetHeroPawnSafe();
                if (hero == null)
                {
                    v.NextTextBlock(ctx, "D2HK_UI_NoneAssignedOverview".Translate(), GameFont.Small, 0f, "NoHero");
                    return;
                }

                int karma = HKRuntime.GetGlobalKarma(hero);
                int standing = HKRuntime.GetGlobalStanding(hero);
                bool showStanding = HKIdeologyCompat.IsStandingEnabled;

                Rect statusRect = v.Next(MeasureStatusPanel(ctx, hero, showStanding, rect.width), UIRectTag.Panel, "Overview/Status");
                DrawStatusPanel(statusRect, hero, karma, standing, showStanding);

                v.NextSpace(ctx.Style.GapM);

                List<HKEffectCard> cards = HKUIData.BuildActiveEffectCards(hero, karma);
                List<HKLedgerRow> deeds = HKRuntime.GetLedgerRows(hero, 8).ToList();

                bool stack = rect.width < 760f;
                if (stack)
                {
                    Rect effectsRect = v.Next(MeasureEffectsPanel(ctx, rect.width, cards), UIRectTag.Panel, "Overview/Effects");
                    DrawEffectsPanel(effectsRect, cards);

                    v.NextSpace(ctx.Style.GapM);

                    Rect deedsRect = v.Next(MeasureDeedsPanel(ctx, rect.width, deeds), UIRectTag.Panel, "Overview/Deeds");
                    DrawDeedsPanel(deedsRect, deeds);
                    return;
                }

                float gap = ctx.Style.Gap;
                float leftWidth = Mathf.Max(ctx.Style.MinClickSize * 6f, Mathf.Floor((rect.width - gap) * 0.60f));
                float rightWidth = Mathf.Max(0f, rect.width - leftWidth - gap);
                float effectsHeight = MeasureEffectsPanel(ctx, leftWidth, cards);
                float deedsHeight = MeasureDeedsPanel(ctx, rightWidth, deeds);
                float rowHeight = Mathf.Max(effectsHeight, deedsHeight);

                Rect rowRect = v.Next(rowHeight, UIRectTag.Group, "Overview/LowerRow");
                var h = new D2HRow(ctx, rowRect);
                Rect leftRect = h.NextFixed(leftWidth, UIRectTag.Panel, "Overview/LowerRow/Left");
                Rect rightRect = h.Remaining(UIRectTag.Panel, "Overview/LowerRow/Right");
                DrawEffectsPanel(leftRect, cards);
                DrawDeedsPanel(rightRect, deeds);
            },
            "OverviewScroll");
    }

    private void DrawReputationTab(Rect rect)
    {
        Pawn hero = HKRuntime.GetHeroPawnSafe();
        var v = Ctx.D2VStack(rect);

        if (hero == null)
        {
            v.NextTextBlock(Ctx, "D2HK_UI_NoneAssignedOverview".Translate(), GameFont.Small, 0f, "NoHero");
            return;
        }

        Rect filterRect = v.NextRow(UIRectTag.Input, "Reputation/FilterRow");
        DrawReputationFilterRow(filterRect);
        v.NextSpace(Ctx.Style.GapS);

        Rect bodyRect = v.NextFill(UIRectTag.Body, "Reputation/Body");
        bool stack = bodyRect.width < 780f;
        if (stack)
        {
            float topHeight = Mathf.Max(180f, Mathf.Floor((bodyRect.height - Ctx.Style.Gap) * 0.44f));
            Rect listRect = new(bodyRect.x, bodyRect.y, bodyRect.width, topHeight);
            Rect detailRect = new(bodyRect.x, listRect.yMax + Ctx.Style.Gap, bodyRect.width, Mathf.Max(0f, bodyRect.yMax - listRect.yMax - Ctx.Style.Gap));
            DrawReputationListPanel(listRect, hero);
            DrawReputationDetailPanel(detailRect, hero);
            return;
        }

        float gap = Ctx.Style.Gap;
        float leftWidth = Mathf.Max(Ctx.Style.MinClickSize * 7f, Mathf.Floor((bodyRect.width - gap) * 0.44f));
        var h = new D2HRow(Ctx, bodyRect);
        Rect listPanel = h.NextFixed(leftWidth, UIRectTag.Panel, "Reputation/ListPanel");
        Rect detailPanel = h.Remaining(UIRectTag.Panel, "Reputation/DetailPanel");
        DrawReputationListPanel(listPanel, hero);
        DrawReputationDetailPanel(detailPanel, hero);
    }

    private void DrawReputationFilterRow(Rect rect)
    {
        string[] labels =
        {
            "D2HK_UI_All".Translate().ToString(),
            "D2HK_UI_People".Translate().ToString(),
            "D2HK_UI_Factions".Translate().ToString()
        };

        float selectorWidth = Mathf.Min(280f, Mathf.Max(rect.width * 0.46f, 220f));
        var h = new D2HRow(Ctx, rect);
        Rect selectorRect = h.NextFixed(selectorWidth, UIRectTag.Input, "Reputation/FilterSelector");
        Rect searchRect = h.Remaining(UIRectTag.Input, "Reputation/Search");

        DrawSelectorRow(selectorRect, labels, ref _repFilterIndex, "Reputation/FilterSelector");
        D2Fields.SearchBoxVanilla(Ctx, searchRect, ref _reputationSearch, "D2HK_UI_Search".Translate().ToString(), showSearchIcon: true, label: "Reputation/Search");
    }

    private void DrawReputationListPanel(Rect rect, Pawn hero)
    {
        List<RepSnapshot> filtered = GetFilteredReputation(hero);
        EnsureSelectedReputation(filtered);

        using var panel = Ctx.GroupPanel("ReputationListPanel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
        var section = D2Section.Layout(Ctx, panel.Inner, new D2Section.Spec("ReputationListSection", headerHeight: SectionHeaderHeight, soft: false, pad: false, drawBackground: false));
        D2Section.DrawCaptionStrip(Ctx, section.Header, "D2HK_UI_Reputation".Translate(), "ReputationList/Title", GameFont.Medium);

        D2ScrollView.Draw(
            Ctx,
            section.Body,
            ref _repListScroll,
            ref _repListContentHeight,
            delegate(UIContext ctx, ref D2VStack inner)
            {
                if (filtered.Count == 0)
                {
                    inner.NextTextBlock(ctx, _reputationSearch.NullOrEmpty() ? "D2HK_UI_ReputationNone".Translate() : "D2HK_UI_ReputationNoMatches".Translate(), GameFont.Small, 0f, "Empty");
                    return;
                }

                for (int i = 0; i < filtered.Count; i++)
                {
                    RepSnapshot snapshot = filtered[i];
                    Rect rowRect = inner.Next(MeasureReputationRowHeight(ctx, section.Body.width, snapshot), UIRectTag.Control_MenuRow, "Reputation/Row[" + i + "]");
                    bool selected = snapshot.targetId == _selectedTargetId && snapshot.isFaction == _selectedIsFaction;
                    if (DrawReputationRow(rowRect, snapshot, "Reputation/Row[" + i + "]", selected, alt: i % 2 == 1))
                    {
                        _selectedTargetId = snapshot.targetId;
                        _selectedIsFaction = snapshot.isFaction;
                    }
                }
            },
            "ReputationListScroll");
    }

    private void DrawReputationDetailPanel(Rect rect, Pawn hero)
    {
        List<RepSnapshot> filtered = GetFilteredReputation(hero);
        EnsureSelectedReputation(filtered);
        RepSnapshot selected = filtered.FirstOrDefault(x => x.targetId == _selectedTargetId && x.isFaction == _selectedIsFaction);

        using var panel = Ctx.GroupPanel("ReputationDetailPanel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
        if (!selected.valid)
        {
            var v = Ctx.D2VStack(panel.Inner);
            v.NextTextBlock(Ctx, "D2HK_UI_SelectReputationTarget".Translate(), GameFont.Small, 0f, "Empty");
            return;
        }

        var section = D2Section.Layout(Ctx, panel.Inner, new D2Section.Spec("ReputationDetailSection", headerHeight: SectionHeaderHeight, soft: false, pad: false, drawBackground: false));
        D2Section.DrawCaptionStrip(Ctx, section.Header, HKUIData.GetReputationDisplayName(selected), "ReputationDetail/Title", GameFont.Medium);

        D2ScrollView.Draw(
            Ctx,
            section.Body,
            ref _repDetailScroll,
            ref _repDetailContentHeight,
            delegate(UIContext ctx, ref D2VStack inner)
            {
                DrawSubsectionLabel(ref inner, "D2HK_UI_Summary".Translate(), "ReputationDetail/SummaryLabel");
                DrawDisplayLines(ref inner, HKUIData.BuildReputationSummaryLines(selected), "ReputationDetail/SummaryLines");

                inner.NextSpace(ctx.Style.GapM);
                DrawSubsectionLabel(ref inner, "D2HK_UI_CurrentEffects".Translate(), "ReputationDetail/EffectsLabel");
                DrawDisplayLines(ref inner, HKUIData.BuildReputationEffectLines(selected, hero), "ReputationDetail/EffectsLines");

                inner.NextSpace(ctx.Style.GapM);
                DrawSubsectionLabel(ref inner, "D2HK_UI_LastChange".Translate(), "ReputationDetail/LastChangeLabel");
                DrawDisplayLines(ref inner, HKUIData.BuildReputationLastChangeLines(selected), "ReputationDetail/LastChangeLines");
            },
            "ReputationDetailScroll");
    }

    private List<RepSnapshot> GetFilteredReputation(Pawn hero)
    {
        string heroId = hero?.GetUniqueLoadID();
        if (heroId.NullOrEmpty())
            return new List<RepSnapshot>();

        List<RepSnapshot> source = HKServices.LocalRep.GetTopReputationEntries(heroId, 128);
        return HKUIData.FilterReputationSnapshots(source, (HKRepFilter)Mathf.Clamp(_repFilterIndex, 0, 2), _reputationSearch);
    }

    private void EnsureSelectedReputation(List<RepSnapshot> filtered)
    {
        if (filtered == null || filtered.Count == 0)
        {
            _selectedTargetId = null;
            _selectedIsFaction = false;
            return;
        }

        bool exists = filtered.Any(x => x.targetId == _selectedTargetId && x.isFaction == _selectedIsFaction);
        if (!exists)
        {
            _selectedTargetId = filtered[0].targetId;
            _selectedIsFaction = filtered[0].isFaction;
        }
    }

    private float MeasureStatusPanel(UIContext ctx, Pawn hero, bool showStanding, float width)
    {
        float innerWidth = Mathf.Max(0f, width - (ctx.Style.Pad * 2f));
        float height = ctx.Style.Pad * 2f;
        bool first = true;

        void Add(float blockHeight)
        {
            if (!first)
                height += ctx.Style.Gap;
            height += blockHeight;
            first = false;
        }

        Add(SectionHeaderHeight);
        Add(ctx.Style.RowHeight);
        Add(D2BandRulerRow.Height(ctx, hasMilestones: true));

        if (showStanding)
        {
            Add(D2BandRulerRow.Height(ctx));
            Add(SubsectionHeaderHeight);
            Add(MeasureDisplayLineListHeight(ctx, innerWidth, HKUIData.BuildStandingEffectLines(hero)));
        }

        return height;
    }

    private void DrawStatusPanel(Rect rect, Pawn hero, int karma, int standing, bool showStanding)
    {
        using var panel = Ctx.GroupPanel("StatusPanel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
        var v = Ctx.D2VStack(panel.Inner);
        DrawPanelTitle(ref v, "D2HK_UI_Status".Translate(), "Status/Title");

        Rect heroRow = v.NextRow(UIRectTag.Input, "Status/HeroRow");
        D2TextPairRow.Draw(Ctx, heroRow, "D2HK_UI_CurrentHero".Translate(), hero.LabelShortCap, id: "Status/HeroRow/TextPair", leftWidthFraction: 0.36f);

        var karmaMilestones = HKUIData.BuildKarmaMilestones(karma);
        Rect karmaRect = v.Next(D2BandRulerRow.Height(Ctx, hasMilestones: karmaMilestones.Count > 0), UIRectTag.Input, "Status/Karma");
        D2BandRulerRow.Draw(Ctx, karmaRect, HKUIConstants.KarmaIcon, "D2HK_UI_Karma".Translate(), HKUIData.KarmaSummary(karma), karma, HKRuntime.KarmaMin, HKRuntime.KarmaMax, HKUIData.KarmaBands, "Status/KarmaRow", "D2HK_UI_KarmaTooltip".Translate(), milestones: karmaMilestones);

        if (!showStanding)
            return;

        Rect standingRect = v.Next(D2BandRulerRow.Height(Ctx), UIRectTag.Input, "Status/Standing");
        D2BandRulerRow.Draw(Ctx, standingRect, HKUIConstants.StandingIcon, "D2HK_UI_Standing".Translate(), HKUIData.StandingSummary(standing), standing, HKRuntime.KarmaMin, HKRuntime.KarmaMax, HKUIData.StandingBands, "Status/StandingRow", "D2HK_UI_StandingTooltip".Translate());

        DrawSubsectionLabel(ref v, "D2HK_UI_CurrentStandingEffects".Translate(), "Status/StandingEffectsLabel");
        DrawDisplayLines(ref v, HKUIData.BuildStandingEffectLines(hero), "Status/StandingEffects");
    }

    private float MeasureEffectsPanel(UIContext ctx, float width, List<HKEffectCard> cards)
    {
        float innerWidth = Mathf.Max(0f, width - (ctx.Style.Pad * 2f));
        float height = ctx.Style.Pad * 2f;
        bool first = true;

        void Add(float blockHeight)
        {
            if (!first)
                height += ctx.Style.Gap;
            height += blockHeight;
            first = false;
        }

        Add(SectionHeaderHeight);
        if (cards == null || cards.Count == 0)
        {
            Add(D2Text.ParagraphHeight(ctx, "D2HK_UI_NoActiveEffects".Translate(), innerWidth, GameFont.Small));
            return height;
        }

        for (int i = 0; i < cards.Count; i++)
            Add(MeasureEffectBlockHeight(ctx, innerWidth, cards[i]));

        return height;
    }

    private void DrawEffectsPanel(Rect rect, List<HKEffectCard> cards)
    {
        using var panel = Ctx.GroupPanel("EffectsPanel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
        var v = Ctx.D2VStack(panel.Inner);
        DrawPanelTitle(ref v, "D2HK_UI_ActiveEffects".Translate(), "Effects/Title");

        if (cards == null || cards.Count == 0)
        {
            v.NextTextBlock(Ctx, "D2HK_UI_NoActiveEffects".Translate(), GameFont.Small, 0f, "Effects/Empty");
            return;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            DrawEffectBlock(ref v, cards[i], "Effects/Block[" + i + "]");
        }
    }

    private float MeasureEffectBlockHeight(UIContext ctx, float width, HKEffectCard card)
    {
        float height = 0f;
        bool first = true;

        void Add(float blockHeight)
        {
            if (!first)
                height += ctx.Style.Gap;
            height += blockHeight;
            first = false;
        }

        Add(ctx.Style.Line);
        Add(D2Text.ParagraphHeight(ctx, card.Description ?? string.Empty, width, GameFont.Small));

        if (card.PrimaryLines.Count > 0)
            Add(MeasureDisplayLineListHeight(ctx, width, card.PrimaryLines));

        if (card.SecondaryLines.Count > 0)
            Add(MeasureDisplayLineListHeight(ctx, width, card.SecondaryLines));

        return height;
    }

    private void DrawEffectBlock(ref D2VStack v, HKEffectCard card, string id)
    {
        Rect titleRect = v.Next(Ctx.Style.Line, UIRectTag.Label, id + "/Title");
        if (card.Icon != null)
        {
            var row = new D2HRow(Ctx, titleRect);
            Rect iconRect = row.Next(Ctx.Style.IconSize, Ctx.Style.IconSize, UIRectTag.Icon, id + "/Icon");
            Rect textRect = row.Remaining(UIRectTag.Label, id + "/TitleTextRect");
            D2Widgets.DrawTextureFitted(Ctx, iconRect.ContractedBy(Ctx.Style.IconInset), card.Icon, id + "/IconDraw");
            using (new TextStateScope(GameFont.Small, TextAnchor.MiddleLeft, false))
                D2Widgets.LabelClippedAligned(Ctx, textRect, card.Title ?? string.Empty, TextAnchor.MiddleLeft, id + "/TitleText");
        }
        else
        {
            using (new TextStateScope(GameFont.Small, TextAnchor.MiddleLeft, false))
                D2Widgets.LabelClippedAligned(Ctx, titleRect, card.Title ?? string.Empty, TextAnchor.MiddleLeft, id + "/TitleText");
        }

        v.NextTextBlock(Ctx, card.Description ?? string.Empty, GameFont.Small, 0f, id + "/Description");
        if (card.PrimaryLines.Count > 0)
            DrawDisplayLines(ref v, card.PrimaryLines, id + "/Primary");
        if (card.SecondaryLines.Count > 0)
            DrawDisplayLines(ref v, card.SecondaryLines, id + "/Secondary");
    }

    private float MeasureDeedsPanel(UIContext ctx, float width, List<HKLedgerRow> deeds)
    {
        float innerWidth = Mathf.Max(0f, width - (ctx.Style.Pad * 2f));
        float height = ctx.Style.Pad * 2f;
        bool first = true;

        void Add(float blockHeight)
        {
            if (!first)
                height += ctx.Style.Gap;
            height += blockHeight;
            first = false;
        }

        Add(SectionHeaderHeight);
        if (deeds == null || deeds.Count == 0)
        {
            Add(D2Text.ParagraphHeight(ctx, "D2HK_UI_NoRecentDeeds".Translate(), innerWidth, GameFont.Small));
            return height;
        }

        for (int i = 0; i < deeds.Count; i++)
            Add(MeasureDeedRowHeight(ctx, innerWidth, deeds[i]));

        return height;
    }

    private void DrawDeedsPanel(Rect rect, List<HKLedgerRow> deeds)
    {
        using var panel = Ctx.GroupPanel("DeedsPanel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
        var v = Ctx.D2VStack(panel.Inner);
        DrawPanelTitle(ref v, "D2HK_UI_RecentDeeds".Translate(), "Deeds/Title");

        if (deeds == null || deeds.Count == 0)
        {
            v.NextTextBlock(Ctx, "D2HK_UI_NoRecentDeeds".Translate(), GameFont.Small, 0f, "Deeds/Empty");
            return;
        }

        for (int i = 0; i < deeds.Count; i++)
        {
            Rect rowRect = v.Next(MeasureDeedRowHeight(Ctx, panel.Inner.width, deeds[i]), UIRectTag.Control_MenuRow, "Deeds/Row[" + i + "]");
            DrawDeedRow(rowRect, deeds[i], "Deeds/Row[" + i + "]", alt: i % 2 == 1);
        }
    }

    private float MeasureDeedRowHeight(UIContext ctx, float width, HKLedgerRow row)
    {
        string title = row.label ?? row.eventKey ?? string.Empty;
        float titleHeight = D2Text.ParagraphHeight(ctx, title, width, GameFont.Small);
        return titleHeight + ctx.Style.GapXS + ctx.Style.Line;
    }

    private void DrawDeedRow(Rect rect, HKLedgerRow row, string id, bool alt)
    {
        Ctx.RecordRect(rect, UIRectTag.Control_MenuRow, id, null);
        if (alt)
            D2Widgets.DrawAltRect(Ctx, rect, id + "/Alt");
        D2Widgets.HighlightOnHover(Ctx, rect, id + "/Hover");

        string tooltip = BuildDeedTooltip(row);
        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        float titleHeight = D2Text.ParagraphHeight(Ctx, row.label ?? row.eventKey ?? string.Empty, rect.width, GameFont.Small);
        Rect titleRect = new(rect.x, rect.y, rect.width, titleHeight);
        Rect bottomRect = new(rect.x, titleRect.yMax + Ctx.Style.GapXS, rect.width, Ctx.Style.Line);

        D2Text.DrawWrappedLabel(Ctx, titleRect, row.label ?? row.eventKey ?? string.Empty, GameFont.Small, UIRectTag.Text_Wrapped, id + "/Title");

        if (row.standingDelta == 0)
        {
            DrawInlineMetric(bottomRect, "D2HK_UI_Karma".Translate().ToString(), HKUIData.FormatSigned(row.delta), HKValueTint.None, id + "/Karma", TextAnchor.MiddleLeft);
            return;
        }

        float gap = Ctx.Style.GapXS;
        float leftWidth = Mathf.Floor((bottomRect.width - gap) * 0.5f);
        Rect leftRect = new(bottomRect.x, bottomRect.y, leftWidth, bottomRect.height);
        Rect rightRect = new(leftRect.xMax + gap, bottomRect.y, Mathf.Max(0f, bottomRect.width - leftWidth - gap), bottomRect.height);
        DrawInlineMetric(leftRect, "D2HK_UI_Karma".Translate().ToString(), HKUIData.FormatSigned(row.delta), HKValueTint.None, id + "/Karma", TextAnchor.MiddleLeft);
        DrawInlineMetric(rightRect, "D2HK_UI_Standing".Translate().ToString(), HKUIData.FormatSigned(row.standingDelta), HKUIData.TintForSigned(row.standingDelta, beneficialWhenPositive: true), id + "/Standing", TextAnchor.MiddleLeft);
    }

    private string BuildDeedTooltip(HKLedgerRow row)
    {
        var parts = new List<string>();
        if (!row.detail.NullOrEmpty())
            parts.Add(row.detail);
        if (!row.reason.NullOrEmpty())
            parts.Add("D2HK_UI_Karma".Translate() + ": " + row.reason);
        if (!row.standingReason.NullOrEmpty())
            parts.Add("D2HK_UI_Standing".Translate() + ": " + row.standingReason);
        return string.Join("\n", parts.Where(x => !x.NullOrEmpty()));
    }

    private void DrawPanelTitle(ref D2VStack v, string title, string id)
    {
        Rect rect = v.Next(SectionHeaderHeight, UIRectTag.Label, id);
        D2Section.DrawCaptionStrip(Ctx, rect, title, id, GameFont.Medium);
    }

    private void DrawSubsectionLabel(ref D2VStack v, string text, string id)
    {
        Rect rect = v.Next(SubsectionHeaderHeight, UIRectTag.Label, id);
        D2Section.DrawCaptionStrip(Ctx, rect, text, id, GameFont.Small);
    }

    private float MeasureDisplayLineListHeight(UIContext ctx, float width, List<HKDisplayLine> lines)
    {
        if (lines == null || lines.Count == 0)
            return 0f;

        float height = 0f;
        for (int i = 0; i < lines.Count; i++)
        {
            HKDisplayLine line = lines[i];
            height += line.IsPair
                ? ctx.Style.RowHeight
                : D2Text.ParagraphHeight(ctx, line.Text ?? string.Empty, width, GameFont.Small);

            if (i < lines.Count - 1)
                height += ctx.Style.Gap;
        }

        return height;
    }

    private void DrawDisplayLines(ref D2VStack v, List<HKDisplayLine> lines, string id)
    {
        if (lines == null || lines.Count == 0)
            return;

        for (int i = 0; i < lines.Count; i++)
        {
            HKDisplayLine line = lines[i];
            if (line.IsPair)
            {
                Rect rowRect = v.NextRow(UIRectTag.Input, id + "/Row[" + i + "]");
                DrawValueRow(rowRect, line.Label, line.Value, line.ValueTint, id + "/Row[" + i + "]", line.Tooltip);
            }
            else
            {
                v.NextTextBlock(Ctx, line.Text ?? string.Empty, GameFont.Small, 0f, id + "/Text[" + i + "]");
            }
        }
    }

    private void DrawValueRow(Rect rect, string left, string right, HKValueTint tint, string id, string tooltip = null, float leftWidthFraction = 0.58f)
    {
        D2TextPairRow.Draw(
            Ctx,
            rect,
            left ?? string.Empty,
            right ?? string.Empty,
            id: id,
            tooltip: tooltip,
            leftWidthFraction: leftWidthFraction,
            rightColor: ResolveTintColor(tint));
    }

    private float MeasureReputationRowHeight(UIContext ctx, float width, RepSnapshot snapshot)
    {
        return ctx.Style.RowHeight;
    }

    private bool DrawReputationRow(Rect rect, RepSnapshot snapshot, string id, bool selected, bool alt)
    {
        Ctx.RecordRect(rect, UIRectTag.Control_MenuRow, id, null);
        if (alt)
            D2Widgets.DrawAltRect(Ctx, rect, id + "/Alt");
        if (selected)
            D2Widgets.HighlightSelected(Ctx, rect, id + "/Selected");
        else
            D2Widgets.HighlightOnHover(Ctx, rect, id + "/Hover");

        bool clicked = D2Widgets.ButtonInvisible(Ctx, rect, id + "/Button");
        string band = HKUIData.GetReputationBand(snapshot);
        string valueText = "(" + HKUIData.FormatSigned(snapshot.score) + ")";
        float valueWidth = Mathf.Clamp(MeasureTextWidth(valueText) + Ctx.Style.TextInsetX, 44f, rect.width * 0.35f);
        float leftWidth = Mathf.Clamp(rect.width * 0.46f, rect.width * 0.30f, rect.width - valueWidth - 36f);

        var row = new D2HRow(Ctx, rect);
        Rect leftRect = row.NextFixed(leftWidth, UIRectTag.Label, id + "/Left");
        Rect rightRect = row.Remaining(UIRectTag.Label, id + "/Right");
        Rect valueRect = new(Mathf.Max(rightRect.x, rightRect.xMax - valueWidth), rightRect.y, Mathf.Min(valueWidth, rightRect.width), rightRect.height);
        Rect bandRect = new(rightRect.x, rightRect.y, Mathf.Max(0f, valueRect.x - rightRect.x - Ctx.Style.GapXS), rightRect.height);

        D2Widgets.LabelClippedAligned(Ctx, leftRect, HKUIData.GetReputationDisplayName(snapshot), TextAnchor.MiddleLeft, id + "/LeftText");
        D2Widgets.LabelClippedAligned(Ctx, bandRect, band, TextAnchor.MiddleRight, id + "/BandText");
        D2Widgets.LabelClippedAligned(Ctx, valueRect, valueText, TextAnchor.MiddleRight, id + "/ValueText", null, ResolveTintColor(HKUIData.TintForSigned(snapshot.score, beneficialWhenPositive: true)));
        return clicked;
    }

    private void DrawInlineMetric(Rect rect, string label, string value, HKValueTint tint, string id, TextAnchor labelAnchor)
    {
        Ctx.RecordRect(rect, UIRectTag.Label, id, null);
        float valueWidth = Mathf.Clamp(MeasureTextWidth(value) + Ctx.Style.TextInsetX, 36f, rect.width * 0.42f);
        Rect valueRect = new(Mathf.Max(rect.x, rect.xMax - valueWidth), rect.y, Mathf.Min(valueWidth, rect.width), rect.height);
        Rect labelRect = new(rect.x, rect.y, Mathf.Max(0f, valueRect.x - rect.x - Ctx.Style.GapXS), rect.height);

        D2Widgets.LabelClippedAligned(Ctx, labelRect, label, labelAnchor, id + "/Label");
        D2Widgets.LabelClippedAligned(Ctx, valueRect, value, TextAnchor.MiddleRight, id + "/Value", null, ResolveTintColor(tint));
    }

    private void DrawSelectorRow(Rect rect, IList<string> labels, ref int selectedIndex, string id)
    {
        if (labels == null || labels.Count == 0)
            return;

        float gap = Ctx.Style.GapXS;
        float width = Mathf.Max(0f, (rect.width - (gap * (labels.Count - 1))) / labels.Count);
        float x = rect.x;
        for (int i = 0; i < labels.Count; i++)
        {
            Rect btnRect = new(x, rect.y, width, rect.height);
            x += width + gap;
            if (D2Selectors.SelectorButton(Ctx, btnRect, labels[i], selectedIndex == i, id: id + "/" + i))
                selectedIndex = i;
        }
    }

    private Color? ResolveTintColor(HKValueTint tint)
    {
        return tint switch
        {
            HKValueTint.Positive => Ctx.Style.PositiveTextColor,
            HKValueTint.Negative => Ctx.Style.NegativeTextColor,
            _ => null,
        };
    }

    private static float MeasureTextWidth(string text)
    {
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, false))
            return Text.CalcSize(text ?? string.Empty).x;
    }
}
