using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Despicable.UIFramework;
using Despicable.UIFramework.Blueprints;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;

namespace Despicable.FacePartsModule.UI;

public sealed class Dialog_D2HeadtypeBlacklist : D2WindowBlueprint
{
    private enum FilterMode
    {
        All = 0,
        Ignored = 1,
        Allowed = 2
    }

    private enum HeadRowStatus
    {
        IgnoredSystem = 0,
        IgnoredDefault = 1,
        IgnoredUser = 2,
        AllowedOverride = 3,
        Allowed = 4
    }

    private static readonly D2UIStyle FaceUiStyle = D2UIStyle.Default.With(s =>
    {
        s.HeaderHeight = 58f;
        s.FooterHeight = 0f;
        s.BodyTopPadY = 6f;
        s.BodyBottomPadY = 6f;
        s.RowHeight = 28f;
        s.ButtonHeight = 28f;
    });

    private const float HeaderLineHeight = 28f;
    private const float HeaderSubtitleHeight = 24f;
    private const float FilterWidth = 240f;
    private const float RowHeight = 72f;
    private const float PreviewWidth = 76f;
    private const float ToggleWidth = 96f;
    private const float InnerPad = 6f;

    private FilterMode _filter = FilterMode.All;
    private string _search = string.Empty;
    private Vector2 _listScroll;
    private float _listContentHeight;

    public Dialog_D2HeadtypeBlacklist()
    {
        draggable = true;
        doCloseX = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
        resizeable = false;
    }

    public override Vector2 InitialSize => new(920f, 720f);

    protected override bool UseBodyScroll => false;
    protected override bool EnableAutoMeasure => false;
    protected override D2UIStyle Style => FaceUiStyle;

    public override void PreClose()
    {
        try
        {
            FacePartsUtil.SaveHeadTypeBlacklist();
        }
        catch (Exception e)
        {
            Log.Error($"[Despicable] Failed to persist head blacklist on close: {e}");
        }

        base.PreClose();
    }

    protected override void DrawHeader(Rect rect)
    {
        int total = DefDatabase<HeadTypeDef>.AllDefsListForReading.Count;
        string subtitle = total == 1 ? "1 headtype" : total + " headtypes";

        var v = Ctx.D2VStack(rect, label: "Header/Stack");
        Rect titleRect = v.Next(HeaderLineHeight, UIRectTag.Label, "Header/Title");
        Rect subtitleRect = v.Next(HeaderSubtitleHeight, UIRectTag.Label, "Header/Subtitle");

        D2Text.DrawWrappedLabel(Ctx, titleRect, "Headtype Blacklist", GameFont.Medium, UIRectTag.Label, "Header/TitleText");
        D2Text.DrawWrappedLabel(Ctx, subtitleRect, subtitle, GameFont.Small, UIRectTag.Label, "Header/SubtitleText");
    }

    protected override void DrawBody(Rect rect)
    {
        var v = Ctx.D2VStack(rect, label: "Body/Stack");
        Rect controlsRect = v.NextRow(UIRectTag.Input, "Body/Controls");
        DrawControlsRow(controlsRect);
        v.NextSpace(Ctx.Style.GapS);

        Rect listRect = v.NextFill(UIRectTag.Body, "Body/ListRect");
        using var panel = Ctx.GroupPanel("HeadtypeListPanel", listRect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
        D2ScrollView.Draw(Ctx, panel.Inner, ref _listScroll, ref _listContentHeight, DrawRows, "HeadtypeListScroll");
    }

    private void DrawControlsRow(Rect rect)
    {
        var h = new D2HRow(Ctx, rect);
        Rect filterRect = h.NextFixed(FilterWidth, UIRectTag.Control_Selector, "Controls/Filter");
        Rect searchRect = h.Remaining(UIRectTag.Control_Search, "Controls/Search");

        string[] labels = { "All", "Ignored", "Allowed" };
        float gap = Ctx.Style.Gap * 0.5f;
        float buttonWidth = Mathf.Max(0f, (filterRect.width - (gap * 2f)) / 3f);
        float x = filterRect.x;
        for (int i = 0; i < labels.Length; i++)
        {
            Rect buttonRect = new(x, filterRect.y, buttonWidth, filterRect.height);
            x += buttonWidth + gap;

            bool selected = (int)_filter == i;
            var spec = new D2Selectors.SelectorSpec("Controls/Filter/" + labels[i], labels[i], selected, false, null, null);
            if (D2Selectors.SelectorButton(Ctx, buttonRect, spec))
                _filter = (FilterMode)i;
        }

        D2Fields.SearchBoxVanilla(Ctx, searchRect, ref _search, "Search headtypes...", showSearchIcon: true, label: "Controls/SearchBox");
    }

    private void DrawRows(UIContext ctx, ref D2VStack v)
    {
        List<HeadTypeDef> heads = BuildFilteredHeads();
        if (heads.Count == 0)
        {
            v.NextTextBlock(ctx, "No headtypes match the current filter.", GameFont.Small, 0f, "List/Empty");
            return;
        }

        for (int i = 0; i < heads.Count; i++)
        {
            Rect rowRect = v.Next(RowHeight, UIRectTag.ListRow, "List/Row[" + i + "]");
            DrawHeadRow(rowRect, heads[i], i);
        }
    }

    private void DrawHeadRow(Rect rowRect, HeadTypeDef headType, int index)
    {
        HeadRowStatus status = GetRowStatus(headType);
        bool ignored = IsIgnoredStatus(status);
        bool interactive = status != HeadRowStatus.IgnoredSystem;

        if ((index % 2) == 1)
            D2Widgets.DrawAltRect(Ctx, rowRect, "List/Row[" + index + "]/Alt");

        D2Widgets.HighlightOnHover(Ctx, rowRect, "List/Row[" + index + "]/Hover");

        Rect inner = rowRect.ContractedBy(InnerPad);
        Rect previewRect = new(inner.x, inner.y, PreviewWidth, inner.height);
        Rect toggleRect = new(inner.xMax - ToggleWidth, inner.y, ToggleWidth, inner.height);
        Rect labelRect = new(previewRect.xMax + Ctx.Style.Gap, inner.y, Mathf.Max(0f, toggleRect.xMin - (previewRect.xMax + (Ctx.Style.Gap * 2f))), inner.height);
        Rect rowButtonRect = new(inner.x, inner.y, Mathf.Max(0f, toggleRect.xMin - inner.x - Ctx.Style.Gap), inner.height);

        DrawPreviewSlot(previewRect, headType, index);
        DrawLabels(labelRect, headType, status, index);
        DrawToggle(toggleRect, rowButtonRect, headType, status, interactive, index);
    }

    private void DrawPreviewSlot(Rect rect, HeadTypeDef headType, int index)
    {
        Ctx.RecordRect(rect, UIRectTag.PanelSoft, "List/Row[" + index + "]/PreviewSlot", null);
        if (Ctx.Pass != UIPass.Draw)
            return;

        Widgets.DrawMenuSection(rect);
        Texture2D texture = FacePreviewCache.ResolveHeadTexture(headType);
        Rect inner = rect.ContractedBy(4f);
        FacePreviewCache.DrawCroppedTexture(Ctx, inner, texture, "List/Row[" + index + "]/Preview", 0f, FacePreviewCache.PreviewAnchor.BottomCenter);
    }

    private void DrawLabels(Rect rect, HeadTypeDef headType, HeadRowStatus status, int index)
    {
        string primary = headType?.LabelCap.ToString();
        if (primary.NullOrEmpty())
            primary = headType?.defName ?? "Unknown headtype";
        string secondary = BuildSecondaryLine(headType, status, primary);

        Rect primaryRect = new(rect.x, rect.y, rect.width, Mathf.Min(28f, rect.height * 0.5f));
        Rect secondaryRect = new(rect.x, primaryRect.yMax, rect.width, Mathf.Max(0f, rect.yMax - primaryRect.yMax));

        using (new TextStateScope(GameFont.Small, TextAnchor.MiddleLeft, false))
            D2Widgets.LabelClipped(Ctx, primaryRect, primary, "List/Row[" + index + "]/Primary", primary);

        if (!secondary.NullOrEmpty())
        {
            using (new GUIColorScope(Color.gray))
            using (new TextStateScope(GameFont.Tiny, TextAnchor.MiddleLeft, false))
                D2Widgets.LabelClipped(Ctx, secondaryRect, secondary, "List/Row[" + index + "]/Secondary", secondary, Color.gray);
        }
    }

    private void DrawToggle(Rect toggleRect, Rect rowButtonRect, HeadTypeDef headType, HeadRowStatus status, bool interactive, int index)
    {
        bool ignored = IsIgnoredStatus(status);
        bool newIgnored = ignored;
        string toggleLabel = ignored ? "Ignored" : "Allowed";

        if (Ctx.Pass == UIPass.Draw)
        {
            using (new GUIEnabledScope(interactive))
            using (new TextStateScope(GameFont.Small, TextAnchor.MiddleLeft, false))
            {
                Widgets.CheckboxLabeled(toggleRect, toggleLabel, ref newIgnored);
            }

            if (interactive && Widgets.ButtonInvisible(rowButtonRect, false))
                newIgnored = !ignored;

            if (status == HeadRowStatus.IgnoredSystem)
                TooltipHandler.TipRegion(toggleRect, "Ignored by system policy.");
        }

        if (newIgnored == ignored)
            return;

        if (newIgnored)
            FacePartsUtil.AddHeadToBlacklist(headType);
        else
            FacePartsUtil.RemoveHeadFromBlacklist(headType);

        FacePartsUtil.SaveHeadTypeBlacklist();
    }

    private List<HeadTypeDef> BuildFilteredHeads()
    {
        IEnumerable<HeadTypeDef> query = DefDatabase<HeadTypeDef>.AllDefsListForReading.Where(h => h != null);

        if (!string.IsNullOrWhiteSpace(_search))
        {
            string needle = _search.Trim();
            query = query.Where(h => MatchesSearch(h, needle));
        }

        query = _filter switch
        {
            FilterMode.Ignored => query.Where(h => IsIgnoredStatus(GetRowStatus(h))),
            FilterMode.Allowed => query.Where(h => !IsIgnoredStatus(GetRowStatus(h))),
            _ => query
        };

        return query
            .OrderBy(h => IsIgnoredStatus(GetRowStatus(h)) ? 0 : 1)
            .ThenBy(h => h.LabelCap.ToString())
            .ThenBy(h => h.defName ?? string.Empty)
            .ToList();
    }

    private static bool MatchesSearch(HeadTypeDef headType, string needle)
    {
        if (headType == null || needle.NullOrEmpty())
            return true;

        return ContainsIgnoreCase(headType.defName, needle)
            || ContainsIgnoreCase(headType.label, needle)
            || ContainsIgnoreCase(headType.modContentPack?.Name, needle)
            || ContainsIgnoreCase(headType.modContentPack?.PackageId, needle);
    }

    private static bool ContainsIgnoreCase(string text, string needle)
    {
        return !text.NullOrEmpty() && text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static HeadRowStatus GetRowStatus(HeadTypeDef headType)
    {
        if (headType == null)
            return HeadRowStatus.Allowed;

        if (FacePartsUtil.IsSystemBlacklisted(headType))
            return HeadRowStatus.IgnoredSystem;

        if (FacePartsUtil.IsDefaultDisabledHead(headType))
            return FacePartsUtil.IsDefaultDisabledHeadExplicitlyAllowed(headType)
                ? HeadRowStatus.AllowedOverride
                : HeadRowStatus.IgnoredDefault;

        return FacePartsUtil.IsHeadBlacklisted(headType)
            ? HeadRowStatus.IgnoredUser
            : HeadRowStatus.Allowed;
    }

    private static bool IsIgnoredStatus(HeadRowStatus status)
    {
        return status == HeadRowStatus.IgnoredSystem
            || status == HeadRowStatus.IgnoredDefault
            || status == HeadRowStatus.IgnoredUser;
    }

    private static string BuildSecondaryLine(HeadTypeDef headType, HeadRowStatus status, string primary)
    {
        string defName = headType?.defName ?? string.Empty;
        string statusText = status switch
        {
            HeadRowStatus.IgnoredSystem => "Ignored by system",
            HeadRowStatus.IgnoredDefault => "Ignored (default off)",
            HeadRowStatus.IgnoredUser => "Ignored",
            HeadRowStatus.AllowedOverride => "Allowed override",
            _ => "Allowed"
        };

        bool showDefName = !defName.NullOrEmpty() && !string.Equals(primary, defName, StringComparison.OrdinalIgnoreCase);
        if (showDefName)
            return defName + "  •  " + statusText;
        return statusText;
    }
}
