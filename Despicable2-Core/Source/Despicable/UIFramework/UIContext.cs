using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework;
public enum UIPass
{
    Draw = 0,
    Measure = 1
}

/// <summary>
/// UIContext is a per-pass helper that:
/// - provides style metrics
/// - offers layout allocators (VStack/HRow)
/// - records rects for validation/debug overlay (Draw pass)
/// - can run a Measure pass where no widgets are emitted but layout can still be computed
///
/// Quick start (vibe-coding friendly):
///
/// - Allocate rects first (VStack/HRow).
/// - Draw second (D2Widgets or manual).
/// - "Content" (wrapped text) should be measured and allocated exact height.
/// - "Containers" (lists/details panels) usually get the leftover space via NextFill.
///
/// Example:
///
///   var ctx = new UIContext(D2UIStyle.Default, registry, "MyWindow", pass);
///   using (ctx.PushScope("Body"))
///   {
///       var v = new VStack(ctx, outRect);
///       v.NextTextBlock(ctx, "Header", GameFont.Medium, padding: 2f);
///       v.NextTextBlock(ctx, "A wrapped paragraph that should not be squashed.", GameFont.Small);
///
///       // Containers go last: give them the leftover space.
///       Rect content = v.NextFill(UIRectTag.Body, "Content");
///       D2ScrollView.Draw(ctx, content, ref scroll, (inner) =>
///       {
///           var innerV = new VStack(ctx, inner);
///           // ... draw list/details/etc.
///       }, label: "MainScroll");
///   }
/// </summary>
public sealed partial class UIContext
{
    public D2UIStyle Style { get; private set; }
    public readonly UIRectRegistry Registry;

    public UIPass Pass { get; private set; } = UIPass.Draw;

    // Full hierarchical scope path, e.g. "DialogFoo/Body/LeftPanel"
    private readonly List<string> _scopes = new(16);

    // Cached scope path to avoid string.Join allocations on every Record().
    private string _scopePathCache = string.Empty;
    private bool _scopePathDirty = true;

    /// <summary>
    /// Maximum yMax encountered during this pass. Used by auto-measure to compute scroll content height.
    /// </summary>
    public float ContentMaxY { get; private set; } = 0f;

    // Coordinate-space offset used for recording rects when drawing inside transformed contexts
    // (e.g. scroll view content coordinates). This is ONLY applied to recorded rects.
    private Vector2 _offset = Vector2.zero;

    public UIContext(D2UIStyle style, UIRectRegistry registry, string rootScope = null, UIPass pass = UIPass.Draw)
    {
        Style = style ?? D2UIStyle.Default;
        Registry = registry;
        Pass = pass;

        _scopes.Clear();
        if (!string.IsNullOrEmpty(rootScope))
            _scopes.Add(rootScope);
        _scopePathDirty = true;
    }

    public void SetPass(UIPass pass)
    {
        Pass = pass;
        ContentMaxY = 0f;
    }

    public System.IDisposable PushPass(UIPass pass, bool resetContentMax = true)
    {
        UIPass prevPass = Pass;
        float prevContentMaxY = ContentMaxY;
        Pass = pass;
        if (resetContentMax)
            ContentMaxY = 0f;
        return new PassScope(this, prevPass, prevContentMaxY);
    }

    /// <summary>
    /// Non-destructive style override for the remainder of the current pass.
    /// </summary>
    public IDisposable PushStyle(D2UIStyle styleOverride)
    {
        if (styleOverride == null)
            return StyleGuard.Noop;

        var prev = Style;
        Style = styleOverride;
        return new StyleGuard(this, prev);
    }

    public IDisposable PushScope(string scopeName)
    {
        if (string.IsNullOrEmpty(scopeName))
            return ScopeGuard.Noop;

        _scopes.Add(scopeName);
        _scopePathDirty = true;
        return new ScopeGuard(this);
    }

    private void PopScope()
    {
        if (_scopes.Count > 0)
            _scopes.RemoveAt(_scopes.Count - 1);

        _scopePathDirty = true;
    }

    /// <summary>
    /// Converts a local-space rect (e.g. scroll-content space) to window-space for recording.
    /// </summary>
    public Rect ToWindow(Rect r)
    {
        r.x += _offset.x;
        r.y += _offset.y;
        return r;
    }

    /// <summary>
    /// Temporarily applies a coordinate-space offset for recorded rects.
    /// Use this inside scroll views: PushOffset(outRect.position - scrollPos)
    /// so content-space records map correctly to window-space.
    /// </summary>
    public OffsetScope PushOffset(Vector2 delta)
    {
        var prev = _offset;
        _offset = prev + delta;
        return new OffsetScope(this, prev);
    }

    public readonly struct OffsetScope : IDisposable
    {
        private readonly UIContext _ctx;
        private readonly Vector2 _prev;

        internal OffsetScope(UIContext ctx, Vector2 prev)
        {
            _ctx = ctx;
            _prev = prev;
        }

        public void Dispose()
        {
            if (_ctx != null)
                _ctx._offset = _prev;
        }
    }

    public string ScopePath
    {
        get
        {
            if (_scopes.Count == 0)
                return string.Empty;

            if (_scopePathDirty)
            {
                _scopePathCache = string.Join("/", _scopes);
                _scopePathDirty = false;
            }

            return _scopePathCache;
        }
    }

    public void Record(Rect rect, UIRectTag tag, string label = null)
    {
        // Track content extent for Measure passes (and Draw too, harmless).
        if (rect.yMax > ContentMaxY)
            ContentMaxY = rect.yMax;

        if (Pass != UIPass.Draw)
            return;

        string full = BuildLabel(label);
        Registry?.Record(ToWindow(rect), tag, full);
    }

    private string BuildLabel(string label)
    {
        string scope = ScopePath;
        if (string.IsNullOrEmpty(scope))
            return label ?? string.Empty;

        if (string.IsNullOrEmpty(label))
            return scope;

        return scope + "/" + label;
    }


    private readonly struct PassScope : IDisposable
    {
        private readonly UIContext _ctx;
        private readonly UIPass _prevPass;
        private readonly float _prevContentMaxY;

        public PassScope(UIContext ctx, UIPass prevPass, float prevContentMaxY)
        {
            _ctx = ctx;
            _prevPass = prevPass;
            _prevContentMaxY = prevContentMaxY;
        }

        public void Dispose()
        {
            if (_ctx == null)
                return;

            _ctx.Pass = _prevPass;
            _ctx.ContentMaxY = _prevContentMaxY;
        }
    }

    private readonly struct ScopeGuard : IDisposable
    {
        private readonly UIContext _ctx;
        public static readonly ScopeGuard Noop = new(null);

        public ScopeGuard(UIContext ctx)
        {
            _ctx = ctx;
        }

        public void Dispose()
        {
            _ctx?.PopScope();
        }
    }

    private readonly struct StyleGuard : IDisposable
    {
        private readonly UIContext _ctx;
        private readonly D2UIStyle _prev;
        public static readonly StyleGuard Noop = new(null, null);

        public StyleGuard(UIContext ctx, D2UIStyle prev)
        {
            _ctx = ctx;
            _prev = prev;
        }

        public void Dispose()
        {
            if (_ctx != null && _prev != null)
                _ctx.Style = _prev;
        }
    }
}
