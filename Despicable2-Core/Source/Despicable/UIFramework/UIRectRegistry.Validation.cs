using UnityEngine;

using Verse;

namespace Despicable.UIFramework;
// Guardrail-Reason: Validation flow stays centralized because layout diagnostics and rule reporting share one traversal.
public sealed partial class UIRectRegistry
{
    private void ValidateCore(D2UIStyle style)
    {
        if (ValidationMode == UIValidationMode.Off)
        {
            return;
        }

        float min = style?.MinClickSize ?? 24f;

        // Basic bounds / size checks
        for (int i = 0; i < _rects.Count; i++)
        {
            var rec = _rects[i];
            var r = rec.Rect;

            if (r.width < 0f || r.height < 0f)
            {
                _issues.Add(new UIRectIssue(UIRectIssueKind.NegativeSize, UIIssueSeverity.Error, r, rec.Tag, rec.Label, "Negative width/height"));
                continue;
            }

            // Hitbox "too small" checks: only for interactive-ish elements.
            if (IsInteractiveTag(rec.Tag) && (r.width < min || r.height < min))
            {
                _issues.Add(new UIRectIssue(UIRectIssueKind.TooSmall, (ValidationMode == UIValidationMode.Strict ? UIIssueSeverity.Error : UIIssueSeverity.Warning), r, rec.Tag, rec.Label,
                    $"Hitbox too small ({r.width:0.#}x{r.height:0.#} < {min:0.#})"));
            }

            // Text/control fit checks: catch clipped headers, squeezed rows, etc.
            // These are reported as Errors even in ErrorsOnly mode so they show up under the default filter.
            if (style != null)
            {
                if (rec.Tag == UIRectTag.Label && r.height + 0.1f < style.Line)
                {
                    _issues.Add(new UIRectIssue(UIRectIssueKind.TooSmall, UIIssueSeverity.Error, r, rec.Tag, rec.Label,
                        $"Label height too small ({r.height:0.#} < Line {style.Line:0.#})"));
                }

                if (rec.Tag == UIRectTag.Button && r.height + 0.1f < style.ButtonHeight)
                {
                    _issues.Add(new UIRectIssue(UIRectIssueKind.TooSmall, UIIssueSeverity.Error, r, rec.Tag, rec.Label,
                        $"Button height too small ({r.height:0.#} < ButtonHeight {style.ButtonHeight:0.#})"));
                }

                if ((rec.Tag == UIRectTag.TextField || rec.Tag == UIRectTag.Input) && r.height + 0.1f < style.ControlHeight)
                {
                    _issues.Add(new UIRectIssue(UIRectIssueKind.TooSmall, UIIssueSeverity.Error, r, rec.Tag, rec.Label,
                        $"Control height too small ({r.height:0.#} < ControlHeight {style.ControlHeight:0.#})"));
                }

                if (rec.Tag == UIRectTag.TextArea && r.height + 0.1f < style.ControlHeight)
                {
                    _issues.Add(new UIRectIssue(UIRectIssueKind.TooSmall, UIIssueSeverity.Error, r, rec.Tag, rec.Label,
                        $"TextArea height too small ({r.height:0.#} < ControlHeight {style.ControlHeight:0.#})"));
                }
            }

            // Wrapped text validation (localization / scale safety):
            // If the caller recorded measured height metadata (mh=..., pad=...), verify the allocated
            // rect is at least as tall as the measured paragraph.
            if (rec.Tag == UIRectTag.Text_Wrapped || rec.Tag == UIRectTag.Text_Bullet)
            {
                if (TryGetMetaFloat(rec.Label, "mh=", out float mh))
                {
                    float pad = 0f;
                    TryGetMetaFloat(rec.Label, "pad=", out pad);

                    float needed = mh + (pad * 2f);
                    if (r.height + 0.1f < needed)
                    {
                        var sev = (ValidationMode == UIValidationMode.Strict) ? UIIssueSeverity.Error : UIIssueSeverity.Warning;
                        _issues.Add(new UIRectIssue(UIRectIssueKind.TooSmall, sev, r, rec.Tag, rec.Label,
                            $"Wrapped text allocated height too small ({r.height:0.#} < {needed:0.#})"));
                    }
                }
            }

            if (!WindowBounds.Overlaps(r) && !WindowBounds.Contains(new Vector2(r.x, r.y)))
            {
                // Loose out-of-bounds detection. Strict mode can tighten later.
                _issues.Add(new UIRectIssue(UIRectIssueKind.OutOfBounds, UIIssueSeverity.Warning, r, rec.Tag, rec.Label, "Rect appears outside window bounds"));
            }

            // Containment check: flag any rect that is not fully contained by its effective clip bounds.
            // Effective clip = window bounds intersected with the nearest "clipping" ancestor container (scroll viewport, panel, etc).
            // IMPORTANT: This must run in ErrorsOnly too. "Rect leaks out of its panel" is one of the most common
            // real-world layout bugs, and it's exactly what the overlay is meant to reveal during day-to-day dev.
            {
                Rect clip = GetEffectiveClipBounds(rec, style);
                if (!RectFullyInside(clip, r))
                {
                    string msg = (clip == WindowBounds) ? "Rect extends outside window bounds" : "Rect extends outside clip bounds";
                    var sev = (ValidationMode == UIValidationMode.Strict) ? UIIssueSeverity.Error : UIIssueSeverity.Warning;
                    _issues.Add(new UIRectIssue(UIRectIssueKind.OutOfBounds, sev, r, rec.Tag, rec.Label, msg));
                }
            }
        }

        // Overlap checks.
        // Naive n^2 becomes very expensive with large lists, so we use a cheap broadphase:
        // - Only consider overlaps where at least one side is interactive.
        // - Sort by yMin and break when yMin exceeds a.yMax.
        var overlapSeverity = UIIssueSeverity.Error;

        _overlapOrder.Clear();
        for (int i = 0; i < _rects.Count; i++)
        {
            if (_rects[i].Tag != UIRectTag.None)
                _overlapOrder.Add(i);
        }

        _overlapOrder.Sort((ia, ib) => _rects[ia].Rect.yMin.CompareTo(_rects[ib].Rect.yMin));

        for (int oi = 0; oi < _overlapOrder.Count; oi++)
        {
            var a = _rects[_overlapOrder[oi]];
            if (a.Tag == UIRectTag.None)
            {
                continue;
            }

            for (int oj = oi + 1; oj < _overlapOrder.Count; oj++)
            {
                var b = _rects[_overlapOrder[oj]];
                if (b.Tag == UIRectTag.None)
                {
                    continue;
                }

                if (b.Rect.yMin > a.Rect.yMax)
                {
                    break;
                }

                if (!IsInteractiveTag(a.Tag) && !IsInteractiveTag(b.Tag))
                {
                    continue;
                }

                if (IsOverlapping(a, b))
                {
                    _issues.Add(new UIRectIssue(
                        UIRectIssueKind.Overlap,
                        overlapSeverity,
                        a.Rect, a.Tag, a.Label,
                        b.Rect, b.Tag, b.Label,
                        "Rects overlap"));
                }
            }
        }
    }

    private static bool IsInteractiveTag(UIRectTag tag)
    {
        switch (tag)
        {
            case UIRectTag.Button:
            case UIRectTag.Icon:
            case UIRectTag.Input:
            case UIRectTag.TooltipHotspot:
            case UIRectTag.ListRow:
            case UIRectTag.ListRowSelected:
            case UIRectTag.Checkbox:
            case UIRectTag.Slider:
            case UIRectTag.TextField:
            case UIRectTag.TextArea:
                return true;
            default:
                return false;
        }
    }

    private static bool RectFullyInside(Rect outer, Rect inner, float eps = 0.1f)
    {
        return inner.xMin + eps >= outer.xMin &&
               inner.yMin + eps >= outer.yMin &&
               inner.xMax - eps <= outer.xMax &&
               inner.yMax - eps <= outer.yMax;
    }

    private static bool TryGetMetaFloat(string label, string key, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(key))
        {
            return false;
        }

        // Metadata is appended as "... | k=v;...". We only parse after the last '|'
        // so hierarchical labels used for clip inference remain stable.
        int pipe = label.LastIndexOf('|');
        if (pipe < 0 || pipe + 1 >= label.Length)
        {
            return false;
        }

        int start = label.IndexOf(key, pipe + 1, System.StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }
        start += key.Length;

        int end = start;
        while (end < label.Length)
        {
            char c = label[end];
            if (c == ';' || c == ' ')
                break;
            end++;
        }

        if (end <= start)
        {
            return false;
        }

        string num = label.Substring(start, end - start);
        return float.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }


    private Rect GetEffectiveClipBounds(in UIRectRecord rec, D2UIStyle style)
    {
        // Start with window bounds.
        Rect clip = WindowBounds;

        // Find the most specific ancestor container that should be treated as a clip bound.
        // ScrollView is a true clip (BeginScrollView). Panels/groups act as "layout bounds" for diagnostics.
        // We pick the deepest (longest label path) ancestor so we don't over-constrain.
        int bestLen = -1;
        Rect best = clip;
        UIRectTag bestTag = UIRectTag.None;

        for (int i = 0; i < _rects.Count; i++)
        {
            var a = _rects[i];
            if (!IsClipContainerTag(a.Tag))
            {
                continue;
            }

            if (a.Label == null || rec.Label == null)
            {
                continue;
            }
            if (!IsAncestorLabel(a.Label, rec.Label))
            {
                continue;
            }

            int len = a.Label.Length;
            if (len > bestLen)
            {
                bestLen = len;
                best = a.Rect;
                bestTag = a.Tag;
            }
        }

        // Panels/groups act as "layout bounds" for diagnostics. Use their INNER content area as the practical bound
        // (otherwise text can spill into padding and still appear "contained"). ScrollViews are true clip bounds and
        // should not be contracted.
        if (style != null && style.Pad > 0f && IsLayoutContainerTag(bestTag))
        {
            best = best.ContractedBy(style.Pad);
        }

        // Intersect with window bounds so we never exceed the window.
        clip = IntersectRects(clip, best);
        return clip;
    }

    private static bool IsLayoutContainerTag(UIRectTag tag)
    {
        switch (tag)
        {
            case UIRectTag.Panel:
            case UIRectTag.PanelSoft:
            case UIRectTag.Group:
            case UIRectTag.Header:
            case UIRectTag.Body:
            case UIRectTag.Footer:
                return true;
            default:
                return false;
        }
    }

    private static bool IsClipContainerTag(UIRectTag tag)
    {
        switch (tag)
        {
            case UIRectTag.ScrollView:
            case UIRectTag.Panel:
            case UIRectTag.PanelSoft:
            case UIRectTag.Group:
            case UIRectTag.Header:
            case UIRectTag.Body:
            case UIRectTag.Footer:
                return true;
            default:
                return false;
        }
    }

    private static Rect IntersectRects(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        if (xMax < xMin || yMax < yMin)
        {
            return new Rect(xMin, yMin, 0f, 0f);
        }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
    private bool IsOverlapping(in UIRectRecord a, in UIRectRecord b)
    {
        // Quick reject
        if (!a.Rect.Overlaps(b.Rect))
        {
            return false;
        }

        // Explicitly allowed tag overlaps
        if (_allowedOverlapPairs.Contains(PairKey(a.Tag, b.Tag)))
        {
            return false;
        }

        // Ignore container vs descendant overlaps (e.g., Panel containing its children)
        if (IgnoreContainerDescendantOverlaps)
        {
            if (IsContainerLike(a.Tag) && IsAncestorLabel(a.Label, b.Label)) return false;
            if (IsContainerLike(b.Tag) && IsAncestorLabel(b.Label, a.Label)) return false;
        }

        // Ignore "soft" overlaps within same parent scope (backgrounds, soft panels, highlights, etc.)
        if (IgnoreSoftOverlapsWithinSameParentScope && a.ParentScope == b.ParentScope)
        {
            if (IsSoftTag(a.Tag) || IsSoftTag(b.Tag)) return false;
        }

        return true;
    }

    private static bool IsAncestorLabel(string ancestorLabel, string descendantLabel)
    {
        if (string.IsNullOrEmpty(ancestorLabel) || string.IsNullOrEmpty(descendantLabel))
        {
            return false;
        }
        if (descendantLabel.Length <= ancestorLabel.Length)
        {
            return false;
        }

        // Must match prefix and be followed by '/'
        // Example: "Win/Body/LeftPanel" is ancestor of "Win/Body/LeftPanel/Search/Icon"
        return descendantLabel.StartsWith(ancestorLabel) && descendantLabel[ancestorLabel.Length] == '/';
    }

    private static bool IsContainerLike(UIRectTag tag)
    {
        switch (tag)
        {
            case UIRectTag.Panel:
            case UIRectTag.PanelSoft:
            case UIRectTag.Group:
            case UIRectTag.ScrollView:
            case UIRectTag.Header:
            case UIRectTag.Body:
            case UIRectTag.Footer:
                return true;
            default:
                return false;
        }
    }

    private static bool IsSoftTag(UIRectTag tag)
    {
        switch (tag)
        {
            case UIRectTag.PanelSoft:
            case UIRectTag.TooltipHotspot:
            case UIRectTag.DebugOverlay:
                return true;
            default:
                return false;
        }
    }

}
