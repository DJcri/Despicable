using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework;
public enum UIRectIssueKind
{
    OutOfBounds,
    Overlap,
    TooSmall,
    NegativeSize
}

public readonly struct UIRectIssue
{
    public readonly UIRectIssueKind Kind;
    public readonly UIIssueSeverity Severity;

    public readonly Rect A;
    public readonly Rect B;

    public readonly UIRectTag TagA;
    public readonly UIRectTag TagB;

    public readonly string LabelA;
    public readonly string LabelB;

    public readonly string Note;

    public bool IsBinary => TagB != UIRectTag.None || !string.IsNullOrEmpty(LabelB) || B.width != 0f || B.height != 0f;

    public UIRectIssue(UIRectIssueKind kind, UIIssueSeverity severity, Rect a, UIRectTag tagA, string labelA, string note)
    {
        Kind = kind;
        Severity = severity;
        A = a;
        B = default;
        TagA = tagA;
        TagB = UIRectTag.None;
        LabelA = labelA ?? string.Empty;
        LabelB = string.Empty;
        Note = note ?? string.Empty;
    }

    public UIRectIssue(UIRectIssueKind kind, UIIssueSeverity severity, Rect a, UIRectTag tagA, string labelA,
        Rect b, UIRectTag tagB, string labelB, string note)
    {
        Kind = kind;
        Severity = severity;
        A = a;
        B = b;
        TagA = tagA;
        TagB = tagB;
        LabelA = labelA ?? string.Empty;
        LabelB = labelB ?? string.Empty;
        Note = note ?? string.Empty;
    }

    public string Describe()
    {
        // Build strings only when needed (logging/tooltip), to avoid per-issue allocations in Validate().
        if (IsBinary)
        {
            return $"{Severity} {Kind}: {TagA} ({LabelA}) vs {TagB} ({LabelB}){(string.IsNullOrEmpty(Note) ? string.Empty : " - " + Note)}";
        }

        return $"{Severity} {Kind}: {TagA} ({LabelA}){(string.IsNullOrEmpty(Note) ? string.Empty : " - " + Note)}";
    }
}

public readonly struct UIRectRecord
{
    public readonly Rect Rect;
    public readonly UIRectTag Tag;

    /// <summary>
    /// Full hierarchical label, typically "Window/Body/LeftPanel/SearchRow/Icon".
    /// </summary>
    public readonly string Label;

    /// <summary>
    /// Parent scope of this rect (label without final segment).
    /// </summary>
    public readonly string ParentScope;

    public UIRectRecord(Rect rect, UIRectTag tag, string label)
    {
        Rect = rect;
        Tag = tag;
        Label = label ?? string.Empty;

        ParentScope = ExtractParentScope(Label);
    }

    private static string ExtractParentScope(string full)
    {
        if (string.IsNullOrEmpty(full)) return string.Empty;
        int idx = full.LastIndexOf('/');
        if (idx <= 0) return string.Empty;
        return full.Substring(0, idx);
    }
}

/// <summary>
/// Per-window registry for rects used in a single draw pass.
/// Designed to be allocation-light and optional in release builds.
/// </summary>
public sealed partial class UIRectRegistry
{
    private readonly List<UIRectRecord> _rects = new(512);
    private readonly List<UIRectIssue> _issues = new(128);
    // Reused scratch list for overlap candidate ordering (reduces allocations).
    private readonly List<int> _overlapOrder = new(512);

    // Optional: allow certain tag pairs to overlap without raising an issue.
    // (Used for things like tooltip hotspots and debug overlays.)
    private readonly HashSet<int> _allowedOverlapPairs = new();

    // Rate-limit logging per window name (ticks).
    private static readonly Dictionary<string, int> lastLogTicksByWindowName = new(32);

    public IReadOnlyList<UIRectRecord> Rects => _rects;
    public IReadOnlyList<UIRectIssue> Issues => _issues;

    public Rect WindowBounds { get; private set; }
    public string WindowName { get; private set; } = string.Empty;

    public UIValidationMode ValidationMode { get; set; } = UIValidationMode.Off;

    /// <summary>
    /// If true: allow overlaps where one rect is an ancestor container of the other
    /// (based on label prefix matching) and at least one side is a container-like tag.
    /// </summary>
    public bool IgnoreContainerDescendantOverlaps { get; set; } = true;

    /// <summary>
    /// If true: allow overlaps within the same parent scope when at least one side is "soft"
    /// (background/panel-soft/highlight/tooltip, etc). This reduces false positives in dense layouts.
    /// </summary>
    public bool IgnoreSoftOverlapsWithinSameParentScope { get; set; } = true;

    public void BeginFrame(string windowName, Rect bounds)
    {
        WindowName = windowName ?? string.Empty;
        WindowBounds = bounds;
        _rects.Clear();
        _issues.Clear();

        // Keep default overlap allowances every frame (cheap, stable).
        _allowedOverlapPairs.Clear();

        // Tooltip hotspots are allowed to overlap most UI elements.
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.Button);
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.Icon);
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.Input);
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.Label);
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.Panel);
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.PanelSoft);
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.ListItem);
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.ListRow);
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.ScrollView);
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.Tab);
        AllowOverlap(UIRectTag.TooltipHotspot, UIRectTag.Group);

        // Debug overlays can overlap anything.
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Custom);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Panel);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.PanelSoft);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Label);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Button);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Icon);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Input);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.ListItem);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.ListRow);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.ScrollView);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Tab);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.TooltipHotspot);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Header);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Body);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Footer);
        AllowOverlap(UIRectTag.DebugOverlay, UIRectTag.Group);
    }

    public void AllowOverlap(UIRectTag a, UIRectTag b)
    {
        int key1 = PairKey(a, b);
        int key2 = PairKey(b, a);
        _allowedOverlapPairs.Add(key1);
        _allowedOverlapPairs.Add(key2);
    }

    private static int PairKey(UIRectTag a, UIRectTag b) => (((int)a) << 16) ^ (int)b;

    public void Record(Rect rect, UIRectTag tag, string label = null)
    {
        // In Off mode we still allow record calls so callers don't have to branch.
        _rects.Add(new UIRectRecord(rect, tag, label));
    }

    public void Validate(D2UIStyle style)
    {
        ValidateCore(style);
    }

    public void MaybeLogIssues(int maxToLog = 12, int minTicksBetweenLogs = 300)
    {
        if (ValidationMode == UIValidationMode.Off) return;
        if (_issues.Count == 0) return;

        int now = GenTicks.TicksAbs;

        if (!lastLogTicksByWindowName.TryGetValue(WindowName, out int last))
            last = -999999;

        if (now - last < minTicksBetweenLogs)
            return;

        lastLogTicksByWindowName[WindowName] = now;

        int count = Math.Min(maxToLog, _issues.Count);
        for (int i = 0; i < count; i++)
        {
            var it = _issues[i];
            if (it.Severity == UIIssueSeverity.Error)
            {
                Log.Error($"[D2UI] {WindowName}: {it.Describe()}");
            }
            else
            {
                Log.Warning($"[D2UI] {WindowName}: {it.Describe()}");
            }
        }

        if (_issues.Count > maxToLog)
            Log.Warning($"[D2UI] {WindowName}: {_issues.Count - maxToLog} more UI issues suppressed.");
    }
    /// <summary>
    /// Clears throttled log timestamps so overlay diagnostics start fresh on the next UI session.
    /// </summary>
    public static void ResetRuntimeState()
    {
        lastLogTicksByWindowName.Clear();
    }

}
