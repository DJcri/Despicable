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
/// <summary>
/// Practical demo window for validating the UI framework.
/// Goals:
/// - intuitive, low-knob controls
/// - covers common widget types (labels, icons, sliders, dropdowns, lists, bullets, text)
/// - zero overlap/overflow when built correctly (except optional overlap sandbox)
/// </summary>
public sealed partial class Dialog_UIFrameworkDemo : D2WindowBlueprint
{
    private enum DemoScenario { Small, Medium, Huge, LongText, Iconless, Mixed }
    private enum DemoMode { A, B, C }
    private enum DemoSize { S, M, L }

    private struct DemoItem
    {
        public string Label;
        public string Description;
        public bool HasIcon;
        public bool Disabled;
    }

    private Vector2 _listScroll;
    private Vector2 _detailsScroll;
    private float _detailsContentHeight;
    private int _selectedIndex = -1;

    private DemoScenario _scenario = DemoScenario.Medium;
    private DemoMode _mode = DemoMode.A;
    private DemoSize _size = DemoSize.M;

    private bool _enabled = true;
    private float _intensity = 50f;
    private string _filter = string.Empty;
    private string _notes = "Type here...";

    private bool _overlapSandbox;
    private string _commandLog = "No command yet.";

    private int _gizmoSelected;

    // When space is tight, we page "Preview" vs "Browser" behind tabs instead of squeezing.
    private int _previewTab;

    private readonly List<DemoItem> _items = new(256);
    private readonly List<DemoItem> _filtered = new(256);

    public Dialog_UIFrameworkDemo()
    {
        doCloseX = true;
        closeOnClickedOutside = true;

        forcePause = false;
        absorbInputAroundWindow = true;

        RebuildItems();
    }

    public override Vector2 InitialSize => new Vector2(980f, 720f);

    // Demo contains its own scroll areas (the list), so avoid nesting a body scroll view.
    protected override bool UseBodyScroll => false;

    // This header is intentionally a single control row. Using fixed header height keeps it
    // framework-native and avoids auto-measuring against a tall synthetic budget rect.
    protected override bool EnableAutoMeasureHeader => false;

    protected override void DrawHeader(Rect rect)
    {
        D2Widgets.LabelClipped(Ctx, rect, "UI Framework Demo", "Header/Title");
    }

    protected override void DrawBody(Rect rect)
    {
        var ctx = Ctx;

        // Compact controls left, preview + list right.
        RectSplit.SplitVertical(rect, rect.width * 0.34f, ctx.Style.Gap, out Rect left, out Rect right);

        DrawControlsColumn(ctx, left);
        DrawPreviewColumn(ctx, right);
    }

}
