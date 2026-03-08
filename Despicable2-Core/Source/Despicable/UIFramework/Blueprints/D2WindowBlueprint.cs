using UnityEngine;
using Verse;
using Despicable.UIFramework;

namespace Despicable.UIFramework.Blueprints;
/// <summary>
/// A base Window that guarantees a clean Header/Body/Footer split and optional body scroll.
/// Intended for new UI work; existing dialogs remain unchanged until later.
///
/// Key features:
/// - Optional auto-measure pass for scroll height when the derived class uses the framework allocators/widgets.
/// - Scope paths for clearer validation output (e.g. "DialogFoo/Body/LeftPanel/SearchRow/Icon").
///
/// Usage:
/// - Override DrawHeader/DrawBody/DrawFooter.
/// - If EnableAutoMeasure is true, DrawBody will run twice per frame (Measure then Draw).
///   Keep DrawBody deterministic and side-effect free when ctx.Pass == Measure.
/// </summary>
public abstract class D2WindowBlueprint : Window
{
    /// <summary>
    /// If true, Body will be drawn inside a scroll view.
    /// </summary>
    protected virtual bool UseBodyScroll => true;

    /// <summary>
    /// If true, runs a Measure pass to compute scroll content height automatically.
    /// Disable if your DrawBody calls raw Verse.Widgets directly (measure would still execute it).
    /// </summary>
    protected virtual bool EnableAutoMeasure => true;

    /// <summary>
    /// If true, runs a Measure pass to compute header height from DrawHeader content.
    /// </summary>
    protected virtual bool EnableAutoMeasureHeader => true;

    /// <summary>
    /// If true, default footer draws an OK button (Close()).
    /// </summary>
    protected virtual bool CloseOnAccept => false;

    protected virtual D2UIStyle Style => D2UIStyle.Default;

    private readonly UIRectRegistry _registry = new();
    private Vector2 _bodyScroll;

    protected UIContext Ctx { get; private set; }

    public override void DoWindowContents(Rect inRect)
    {
        // Guard against IMGUI state leaks (GUI.color, GUI.enabled, Text.*) causing flicker/invisible controls.
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
        // Frame init
        _registry.ValidationMode = UIValidationMode.Off;
        _registry.BeginFrame(GetType().Name, new Rect(0f, 0f, inRect.width, inRect.height));

        Ctx = new UIContext(Style, _registry, GetType().Name, UIPass.Draw);

        // Allocate sections
        float pad = Ctx.Style.Pad;
        Rect full = new(0f, 0f, inRect.width, inRect.height);

        // Header
        float headerHeight = Ctx.Style.HeaderHeight;
        if (EnableAutoMeasureHeader)
            headerHeight = Mathf.Max(headerHeight, AutoMeasureHeaderHeight(full));

        Rect header = new(full.xMin, full.yMin, full.width, headerHeight);
        _registry.Record(header, UIRectTag.Header, $"{GetType().Name}/Header");
        using (Ctx.PushScope("Header"))
            DrawHeader(ContractRect(header, pad, Ctx.Style.HeaderPadY));

        // Footer
        Rect footer = new(full.xMin, full.yMax - Ctx.Style.FooterHeight, full.width, Ctx.Style.FooterHeight);
        _registry.Record(footer, UIRectTag.Footer, $"{GetType().Name}/Footer");
        using (Ctx.PushScope("Footer"))
            DrawFooter(ContractRect(footer, pad, Ctx.Style.FooterPadY));

        // Body is everything in between, never overlaps header/footer.
        float bodyY = header.yMax;
        float bodyH = Mathf.Max(0f, footer.yMin - bodyY);
        Rect body = new(full.xMin, bodyY, full.width, bodyH);
        _registry.Record(body, UIRectTag.Body, $"{GetType().Name}/Body");

        using (Ctx.PushScope("Body"))
        {
            if (UseBodyScroll)
            {
                var outRect = ContractRect(body, Ctx.Style.EffectiveBodyPadX, Ctx.Style.EffectiveBodyTopPadY, Ctx.Style.EffectiveBodyBottomPadY);

                float contentH = Mathf.Max(outRect.height, GetBodyContentHeight(outRect));
                if (EnableAutoMeasure)
                    contentH = Mathf.Max(contentH, AutoMeasureBodyHeight(outRect));

                // NOTE: RimWorld scrollview requires view rect to start at 0,0.
                Rect view = new(0f, 0f, outRect.width - 16f, contentH);

                _registry.Record(outRect, UIRectTag.ScrollView, $"{GetType().Name}/Body/ScrollOut");
                _registry.Record(view, UIRectTag.ScrollView, $"{GetType().Name}/Body/ScrollView");

                Widgets.BeginScrollView(outRect, ref _bodyScroll, view);
                try
                {
                    // Ensure recorded rects are in window-space while drawing scroll contents.
                    using (Ctx.PushOffset(outRect.position - _bodyScroll))
                    {
                        DrawBody(new Rect(0f, 0f, view.width, view.height));
                    }
                }
                finally
                {
                    Widgets.EndScrollView();
                }
            }
            else
            {
                DrawBody(ContractRect(body, Ctx.Style.EffectiveBodyPadX, Ctx.Style.EffectiveBodyTopPadY, Ctx.Style.EffectiveBodyBottomPadY));
            }
        }

        // Validate + overlay
        _registry.Validate(Ctx.Style);

        }
    }

    private static Rect ContractRect(Rect r, float padX, float padY)
        => ContractRect(r, padX, padY, padY);

    private static Rect ContractRect(Rect r, float padX, float padTopY, float padBottomY)
    {
        float x = r.x + padX;
        float y = r.y + padTopY;
        float w = Mathf.Max(0f, r.width - padX * 2f);
        float h = Mathf.Max(0f, r.height - padTopY - padBottomY);
        return new Rect(x, y, w, h);
    }


    /// <summary>
    /// Override to provide a stable header height. If unknown, return Style.HeaderHeight.
    /// Auto-measure can replace this when you use the framework allocators/widgets.
    /// </summary>
    protected virtual float GetHeaderContentHeight(Rect outRect) => Style.HeaderHeight;

    /// <summary>
    /// Auto-measure by running DrawHeader in a Measure pass (no widgets emitted).
    /// Uses a generous but finite height budget so the header can size to wrapped text.
    /// </summary>
    protected virtual float AutoMeasureHeaderHeight(Rect outerRect)
    {
        var measureCtx = new UIContext(Style, null, GetType().Name, UIPass.Measure);
        using (measureCtx.PushScope("Header"))
        {
            var old = Ctx;
            Ctx = measureCtx;

            try
            {
                float budget = Mathf.Max(Style.HeaderHeight, 256f);
                var measureRect = new Rect(
                    0f,
                    0f,
                    Mathf.Max(0f, outerRect.width - (Style.Pad * 2f)),
                    Mathf.Max(0f, budget - (Style.HeaderPadY * 2f)));
                DrawHeader(measureRect);
            }
            finally
            {
                Ctx = old;
            }
        }

        float measured = Mathf.Max(GetHeaderContentHeight(outerRect), measureCtx.ContentMaxY);
        return Mathf.Max(Style.HeaderHeight, measured + (Style.HeaderPadY * 2f));
    }

    /// <summary>
    /// Override to provide a stable scroll height. If unknown, return outRect.height.
    /// Auto-measure can replace this when you use the framework's layout allocators/widgets.
    /// </summary>
    protected virtual float GetBodyContentHeight(Rect outRect) => outRect.height;

    /// <summary>
    /// Auto-measure by running DrawBody in a Measure pass (no widgets emitted).
    /// For best results: build your body using Ctx + VStack/HRow + D2Widgets so rects are recorded.
    /// </summary>
    protected virtual float AutoMeasureBodyHeight(Rect outRect)
    {
        // Create a measure context that doesn't write to the registry.
        var measureCtx = new UIContext(Style, null, GetType().Name, UIPass.Measure);
        using (measureCtx.PushScope("Body"))
        {
            // Swap Ctx so derived code can keep using Ctx without branching.
            var old = Ctx;
            Ctx = measureCtx;

            try
            {
                // Give an absurd height budget. Layout allocators should advance y and update ContentMaxY.
                var measureRect = new Rect(0f, 0f, outRect.width - 16f, 100000f);
                DrawBody(measureRect);
            }
            finally
            {
                Ctx = old;
            }
        }

        // Ensure at least the outRect height.
        return Mathf.Max(outRect.height, measureCtx.ContentMaxY);
    }

    protected virtual void DrawHeader(Rect rect) { }
    protected abstract void DrawBody(Rect rect);

    protected virtual void DrawFooter(Rect rect)
    {
        if (!CloseOnAccept) return;
        if (Widgets.ButtonText(rect, "OK"))
            Close();
    }
}
