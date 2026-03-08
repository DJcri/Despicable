using System;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework;
/// <summary>
/// Text measurement + drawing helpers that are safe to use in Measure/Draw passes.
///
/// DESIGN INTENT
/// - Make text height deterministic (localization-proof)
/// - Make wrapped paragraphs easy to allocate (no more "guess 70f")
/// - Keep Verse.Text global state (Font, WordWrap, Anchor) well-scoped and restored
///
/// This file is intentionally "pure": it shouldn't depend on Window/ITab/etc.
/// Higher-level helpers (VStack.NextTextBlock, etc.) can call into this.
/// </summary>
public static class D2Text
{
    /// <summary>
    /// Tiny RAII helper to scope Verse.Text global state.
    /// </summary>
    private readonly struct TextStateScope : IDisposable
    {
        private readonly GameFont _font;
        private readonly TextAnchor _anchor;
        private readonly bool _wordWrap;

        public TextStateScope(GameFont font, TextAnchor anchor, bool wordWrap)
        {
            _font = Text.Font;
            _anchor = Text.Anchor;
            _wordWrap = Text.WordWrap;

            Text.Font = font;
            Text.Anchor = anchor;
            Text.WordWrap = wordWrap;
        }

        public void Dispose()
        {
            Text.Font = _font;
            Text.Anchor = _anchor;
            Text.WordWrap = _wordWrap;
        }
    }

    /// <summary>
    /// Measures wrapped height for the given text at the provided width.
    ///
    /// NOTE:
    /// - Verse.Text.CalcHeight respects current Font and WordWrap
    /// - width must be clamped non-negative
    ///
    /// PSEUDOCODE FUTURE (optional caching):
    /// - cache key: (font, width bucket, text hash)
    /// - invalidate cache per-frame OR keep a small LRU (to avoid huge memory)
    /// </summary>
    public static float MeasureWrappedHeight(string text, float width, GameFont font = GameFont.Small)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        float w = Mathf.Max(0f, width);

        using (new TextStateScope(font, TextAnchor.UpperLeft, wordWrap: true))
        {
            return Text.CalcHeight(text, w);
        }
    }

    /// <summary>
    /// Measures a single-line height for the font (conservative).
    /// Use for minimum row heights and vertical centering.
    /// </summary>
    public static float LineHeight(GameFont font = GameFont.Small)
    {
        using (new TextStateScope(font, TextAnchor.UpperLeft, wordWrap: false))
        {
            return Text.LineHeight;
        }
    }

    public static float MeasureWrappedHeight(TaggedString text, float width, GameFont font = GameFont.Small)
    {
        // TaggedString is a Verse type used for translation keys and rich strings.
        // For measurement/drawing, treat it as its resolved string.
        return MeasureWrappedHeight(text.ToString(), width, font);
    }

    /// <summary>
    /// Draws a wrapped label.
    /// - Records the rect under Text_Wrapped (plus optional tag override)
    /// - Safe in Measure pass (no-op)
    ///
    /// PSEUDOCODE:
    /// if ctx.Measure: return
    /// set font + wrap
    /// Widgets.Label(rect, text)
    /// restore state
    /// </summary>
    public static void DrawWrappedLabel(UIContext ctx, Rect rect, string text, GameFont font = GameFont.Small, UIRectTag tag = UIRectTag.Text_Wrapped, string labelForOverlay = null)
        => DrawWrappedLabel(ctx, rect, text, font, tag, labelForOverlay, recordRect: true);

    public static void DrawWrappedLabel(UIContext ctx, Rect rect, TaggedString text, GameFont font = GameFont.Small, UIRectTag tag = UIRectTag.Text_Wrapped, string labelForOverlay = null)
        => DrawWrappedLabel(ctx, rect, text.ToString(), font, tag, labelForOverlay, recordRect: true);

    public static void DrawWrappedLabelKey(UIContext ctx, Rect rect, string key, GameFont font = GameFont.Small, UIRectTag tag = UIRectTag.Text_Wrapped, string labelForOverlay = null, params object[] args)
        => DrawWrappedLabel(ctx, rect, args != null && args.Length > 0 ? key.Translate(args).ToString() : key.Translate().ToString(), font, tag, labelForOverlay ?? key, recordRect: true);


    /// <summary>
    /// Variant that can skip registry recording (useful when a parent allocator already recorded the rect).
    /// </summary>
    public static void DrawWrappedLabel(UIContext ctx, Rect rect, string text, GameFont font, UIRectTag tag, string labelForOverlay, bool recordRect)
    {
        if (recordRect)
            ctx?.RecordRect(rect, tag, labelForOverlay ?? text);

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return;

        using (new TextStateScope(font, TextAnchor.UpperLeft, wordWrap: true))
        {
            Widgets.Label(rect, text ?? string.Empty);
        }
    }

    /// <summary>
    /// Utility used by some higher-level helpers: returns the ideal rect height for a paragraph,
    /// padded to at least the style's line height.
    ///
    /// PSEUDOCODE:
    /// h = MeasureWrappedHeight(text, width, font)
    /// h = max(h, LineHeight(font))
    /// return h
    /// </summary>
    public static float ParagraphHeight(UIContext ctx, TaggedString text, float width, GameFont font = GameFont.Small)
    {
        return ParagraphHeight(ctx, text.ToString(), width, font);
    }

    public static float ParagraphHeight(UIContext ctx, string text, float width, GameFont font = GameFont.Small)
    {
        float h = MeasureWrappedHeight(text, width, font);
        float min = LineHeight(font);
        return Mathf.Max(h, min);
    }
}
