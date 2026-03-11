using System.Collections.Generic;
using UnityEngine;
using Verse;
using Despicable.UIFramework;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Vertical allocator.
/// Keeps a running cursor; all Next* methods return a rect and advance the cursor.
/// </summary>
public struct VStack
{
    private readonly UIContext _ctx;
    private Rect _bounds;
    private float _y;

    public Rect Bounds => _bounds;
    /// <summary>
    /// Current cursor Y (in the same coordinate space as Bounds).
    /// Useful for measuring how much vertical space was consumed.
    /// </summary>
    public float CursorY => _y;

    /// <summary>
    /// Total vertical space consumed so far.
    /// </summary>
    public float UsedHeight => _y - _bounds.yMin;
    public float RemainingHeight => _bounds.yMax - _y;

    public VStack(UIContext ctx, Rect bounds)
    {
        _ctx = ctx;
        _bounds = bounds;
        _y = bounds.yMin;
    }

    private Rect NextAlloc(float height)
    {
        if (height < 0f) height = 0f;
        var r = new Rect(_bounds.xMin, _y, _bounds.width, height);
        _y = r.yMax + _ctx.Style.Gap;
        return r;
    }

    public Rect Next(float height, UIRectTag tag = UIRectTag.None, string label = null)
    {
        var r = NextAlloc(height);
        _ctx.Record(r, tag, label);
        return r;
    }

    /// <summary>
    /// Allocate a row and record it with extra diagnostic metadata (appended to the label).
    /// Use this for cases where validation benefits from known facts (e.g. wrapped text measured height).
    /// </summary>
    public Rect NextWithMeta(float height, UIRectTag tag, string label, string meta)
    {
        var r = NextAlloc(height);
        _ctx.RecordRect(r, tag, label, meta);
        return r;
    }

    public Rect NextLine(UIRectTag tag = UIRectTag.None, string label = null)
        => Next(_ctx.Style.Line, tag, label);

    /// <summary>
    /// Allocate a canonical single-line control row.
    /// Prefer this over NextLine() for checkboxes, dropdowns, sliders, etc.
    /// </summary>
    public Rect NextRow(UIRectTag tag = UIRectTag.None, string label = null)
        => Next(_ctx.Style.RowHeight, tag, label);

    public Rect NextButton(UIRectTag tag = UIRectTag.Button, string label = null)
        => Next(_ctx.Style.ButtonHeight, tag, label);

    public Rect NextDivider(float thickness = 1f, string label = null)
        => Next(thickness, UIRectTag.Divider, label);

    /// <summary>
    /// Allocate empty vertical space (still advances the cursor and applies the standard gap).
    /// This is a semantic alias for Next(height) that keeps call sites readable.
    /// </summary>
    public Rect NextSpace(float height)
        => Next(height, UIRectTag.None, null);

    /// <summary>
    /// Measure a wrapped label height and allocate exactly enough vertical space.
    /// Useful for long translated strings.
    /// </summary>
    public Rect NextWrappedText(string text, float? widthOverride = null, UIRectTag tag = UIRectTag.Label, string label = null)
    {
        float w = widthOverride ?? _bounds.width;

        // IMPORTANT:
        // - Text.CalcHeight depends on the current font state.
        // - Most wrapped labels are drawn via D2Widgets.Label (GameFont.Small, wordwrap on).
        // Measure under the same text state so allocation matches drawing.
        float h;
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            h = Text.CalcHeight(text ?? string.Empty, w);
        }

        // Keep one-line wrapped labels from collapsing below the canonical line height.
        // This avoids cramped layouts and prevents "label too small" false positives.
        if (_ctx != null && _ctx.Style != null)
            h = Mathf.Max(_ctx.Style.Line, h);

        return Next(h, tag, label);
    }

    /// <summary>
    /// Allocate AND draw a wrapped text block with deterministic measurement.
    ///
    /// This is the preferred API for paragraphs going forward.
    /// It prevents the common "measure in one place, draw in another" drift.
    ///
    /// In Measure pass:
    /// - measures wrapped height via D2Text
    /// - allocates the rect (and records it)
    ///
    /// In Draw pass:
    /// - draws the wrapped label inside optional padding
    ///
    /// Use this for paragraphs and anything that needs to wrap (especially translations).
    /// For panels that should occupy leftover space, use NextFill instead.
    /// </summary>
    /// <summary>
    /// Allocate AND draw a wrapped text block with deterministic measurement.
    /// Width is taken from the provided width parameter.
    /// </summary>
    public Rect NextTextBlock(string text, float width, GameFont font, float padding = 0f, string label = null, UIRectTag tag = UIRectTag.Text_Wrapped)
    {
        float innerW = Mathf.Max(0f, width - (padding * 2f));
        float innerH = D2Text.ParagraphHeight(_ctx, text, innerW, font);
        float totalH = innerH + (padding * 2f);
        string meta = $"mh={innerH:0.##};w={innerW:0.##};pad={padding:0.##};f={font}";
        Rect outer = NextWithMeta(totalH, tag, label, meta);
        if (_ctx != null && _ctx.Pass == UIPass.Draw)
        {
            Rect inner = padding > 0f ? outer.ContractedBy(padding) : outer;
            D2Text.DrawWrappedLabel(_ctx, inner, text, font, tag, labelForOverlay: label, recordRect: false);
        }
        return outer;
    }

    /// <inheritdoc cref="NextTextBlock(string,float,GameFont,float,string,UIRectTag)"/>
    [System.Obsolete("Pass ctx is redundant — the VStack already holds it. Use NextTextBlock(text, width, font, ...) instead.")]
    public Rect NextTextBlock(UIContext ctx, string text, float width, GameFont font, float padding = 0f, string label = null, UIRectTag tag = UIRectTag.Text_Wrapped)
    {
        if (ctx == null) ctx = _ctx;
        float innerW = Mathf.Max(0f, width - (padding * 2f));
        float innerH = D2Text.ParagraphHeight(ctx, text, innerW, font);
        float totalH = innerH + (padding * 2f);
        string meta = $"mh={innerH:0.##};w={innerW:0.##};pad={padding:0.##};f={font}";
        Rect outer = NextWithMeta(totalH, tag, label, meta);
        if (ctx != null && ctx.Pass == UIPass.Draw)
        {
            Rect inner = padding > 0f ? outer.ContractedBy(padding) : outer;
            D2Text.DrawWrappedLabel(ctx, inner, text, font, tag, labelForOverlay: label, recordRect: false);
        }
        return outer;
    }

    /// <summary>
    /// Allocate AND draw a wrapped text block using the VStack's current width.
    /// </summary>
    public Rect NextTextBlock(string text, GameFont font, float padding = 0f, string label = null, UIRectTag tag = UIRectTag.Text_Wrapped)
        => NextTextBlock(text, _bounds.width, font, padding, label, tag);

    /// <inheritdoc cref="NextTextBlock(string,GameFont,float,string,UIRectTag)"/>
    [System.Obsolete("Pass ctx is redundant — the VStack already holds it. Use NextTextBlock(text, font, ...) instead.")]
    public Rect NextTextBlock(UIContext ctx, string text, GameFont font, float padding = 0f, string label = null, UIRectTag tag = UIRectTag.Text_Wrapped)
    {
        float w = _bounds.width;
        return NextTextBlock(ctx, text, w, font, padding, label, tag);
    }

    /// <summary>
    /// TaggedString overload (translation keys / rich strings).
    /// </summary>
    public Rect NextTextBlock(TaggedString text, GameFont font, float padding = 0f, string label = null, UIRectTag tag = UIRectTag.Text_Wrapped)
        => NextTextBlock(text.ToString(), _bounds.width, font, padding, label, tag);

    /// <inheritdoc cref="NextTextBlock(TaggedString,GameFont,float,string,UIRectTag)"/>
    [System.Obsolete("Pass ctx is redundant — the VStack already holds it. Use NextTextBlock(text, font, ...) instead.")]
    public Rect NextTextBlock(UIContext ctx, TaggedString text, GameFont font, float padding = 0f, string label = null, UIRectTag tag = UIRectTag.Text_Wrapped)
    {
        float w = _bounds.width;
        return NextTextBlock(ctx, text.ToString(), w, font, padding, label, tag);
    }

    /// <summary>
    /// Allocate and draw a bullet list where each item can wrap.
    /// Each bullet item is recorded as its own rect for overlay/validation.
    ///
    /// Use this instead of manually doing "•" + NextTextBlock in a loop.
    /// </summary>
    /// <summary>
    /// Allocate and draw a bullet list where each item can wrap.
    /// </summary>
    public void NextBulletList(IEnumerable<TaggedString> items, GameFont font = GameFont.Small, float padding = 0f, float bulletIndent = 18f, float bulletGap = 4f, UIRectTag tag = UIRectTag.Text_Bullet, string labelPrefix = "Bullet")
        => NextBulletList(_ctx, items, font, padding, bulletIndent, bulletGap, tag, labelPrefix);

    /// <inheritdoc cref="NextBulletList(IEnumerable{TaggedString},GameFont,float,float,float,UIRectTag,string)"/>
    [System.Obsolete("Pass ctx is redundant — the VStack already holds it. Use NextBulletList(items, ...) instead.")]
    public void NextBulletList(UIContext ctx, IEnumerable<TaggedString> items, GameFont font = GameFont.Small, float padding = 0f, float bulletIndent = 18f, float bulletGap = 4f, UIRectTag tag = UIRectTag.Text_Bullet, string labelPrefix = "Bullet")
    {
        if (items == null) return;
        if (ctx == null) ctx = _ctx;

        int i = 0;
        foreach (var it in items)
        {
            string s = it.ToString();
            float innerW = Mathf.Max(0f, _bounds.width - (padding * 2f) - bulletIndent - bulletGap);
            float innerH = D2Text.ParagraphHeight(ctx, s, innerW, font);
            float totalH = innerH + (padding * 2f);

            string meta = $"mh={innerH:0.##};w={innerW:0.##};pad={padding:0.##};f={font};bi={bulletIndent:0.##}";
            Rect outer = NextWithMeta(totalH, tag, labelPrefix + "[" + i + "]", meta);
            if (ctx != null && ctx.Pass == UIPass.Draw)
            {
                Rect inner = padding > 0f ? outer.ContractedBy(padding) : outer;

                // Bullet glyph column
                Rect bulletRect = new(inner.xMin, inner.yMin, bulletIndent, inner.height);
                Rect textRect = new(inner.xMin + bulletIndent + bulletGap, inner.yMin, Mathf.Max(0f, inner.width - bulletIndent - bulletGap), inner.height);

                using (new TextStateScope(font, TextAnchor.UpperLeft, wordWrap: false))
                {
                    Widgets.Label(bulletRect, "•");
                }

                // Skip recording here since the allocator already recorded outer.
                D2Text.DrawWrappedLabel(ctx, textRect, s, font, tag, labelForOverlay: labelPrefix + "[" + i + "]/Text", recordRect: false);
            }

            i++;
        }
    }

    /// <summary>
    /// Standard single-row selector group with equal-width options.
    /// Returns the new selected index (or the old one if unchanged).
    /// </summary>
    /// <summary>
    /// Standard single-row selector group with equal-width options.
    /// Returns the new selected index (or the old one if unchanged).
    /// </summary>
    public int NextSelectorRow(string groupId, IList<string> options, int selectedIndex, out bool changed, bool allowDeselect = false, UIRectTag tag = UIRectTag.Control_Selector)
        => NextSelectorRow(_ctx, groupId, options, selectedIndex, out changed, allowDeselect, tag);

    /// <inheritdoc cref="NextSelectorRow(string,IList{string},int,out bool,bool,UIRectTag)"/>
    [System.Obsolete("Pass ctx is redundant — the VStack already holds it. Use NextSelectorRow(groupId, options, ...) instead.")]
    public int NextSelectorRow(UIContext ctx, string groupId, IList<string> options, int selectedIndex, out bool changed, bool allowDeselect = false, UIRectTag tag = UIRectTag.Control_Selector)
    {
        changed = false;
        if (options == null || options.Count == 0) return selectedIndex;
        if (ctx == null) ctx = _ctx;

        Rect rowRect = NextRow(tag, groupId);

        if (ctx.Pass != UIPass.Draw)
            return selectedIndex;

        int n = options.Count;
        float gap = (_ctx != null && _ctx.Style != null) ? (_ctx.Style.Gap * 0.5f) : 3f;
        float totalGap = gap * (n - 1);
        float wEach = Mathf.Max(0f, (rowRect.width - totalGap) / n);

        float x = rowRect.xMin;
        int newIndex = selectedIndex;

        for (int i = 0; i < n; i++)
        {
            Rect r = new(x, rowRect.yMin, wEach, rowRect.height);
            x += wEach + gap;

            bool isSel = (i == selectedIndex);
            bool clicked = D2Selectors.SelectorButton(ctx, r, options[i], isSel, disabled: false, disabledReason: null, tooltip: null, id: groupId + "/" + i);

            if (clicked)
            {
                if (allowDeselect && isSel)
                    newIndex = -1;
                else
                    newIndex = i;
            }
        }

        if (newIndex != selectedIndex)
        {
            selectedIndex = newIndex;
            changed = true;
        }

        return selectedIndex;
    }

    public Rect Peek(float height)
    {
        if (height < 0f) height = 0f;
        return new Rect(_bounds.xMin, _y, _bounds.width, height);
    }

    public void Skip(float height)
    {
        if (height < 0f) height = 0f;
        _y += height + _ctx.Style.Gap;
    }

    public Rect Remaining(UIRectTag tag = UIRectTag.Body, string label = null)
    {
        var r = new Rect(_bounds.xMin, _y, _bounds.width, Mathf.Max(0f, _bounds.yMax - _y));
        _y = _bounds.yMax;
        _ctx.Record(r, tag, label);
        return r;
    }

    /// <summary>
    /// Allocate the remainder of the available vertical space.
    /// This is a semantic alias for Remaining(...) that reads better at call sites,
    /// especially for "fill the rest" panels (scroll views, grids, detail panes).
    ///
    /// Rule of thumb:
    /// - Use NextFill for CONTAINERS.
    /// - Do NOT use NextFill for measured content like paragraphs; use NextTextBlock/NextBulletList.
    /// </summary>
    public Rect NextFill(UIRectTag tag = UIRectTag.Body, string label = null)
        => Remaining(tag, label);
}
