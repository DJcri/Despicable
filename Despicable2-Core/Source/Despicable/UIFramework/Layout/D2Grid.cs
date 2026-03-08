using UnityEngine;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Simple framework-owned grid allocator.
///
/// Purpose:
/// - allocate repeatable tile / card / gizmo cells
/// - keep sizing and placement in one reusable primitive
/// - avoid ad hoc row/column rect math at call sites
///
/// This is intentionally small and deterministic. It does not draw.
/// It only computes framework-controlled cell rects inside an outer rect.
/// </summary>
public struct D2Grid
{
    private readonly Rect _outer;
    private readonly int _cols;
    private readonly int _rows;
    private readonly float _gap;
    private readonly float _padding;
    private readonly float _cellW;
    private readonly float _cellH;

    private D2Grid(Rect outer, int cols, int rows, float gap, float padding)
    {
        _outer = outer;
        _cols = Mathf.Max(1, cols);
        _rows = Mathf.Max(1, rows);
        _gap = Mathf.Max(0f, gap);
        _padding = Mathf.Max(0f, padding);

        float innerW = Mathf.Max(0f, outer.width - (_padding * 2f));
        float innerH = Mathf.Max(0f, outer.height - (_padding * 2f));

        _cellW = Mathf.Max(0f, (innerW - (_gap * (_cols - 1))) / _cols);
        _cellH = Mathf.Max(0f, (innerH - (_gap * (_rows - 1))) / _rows);
    }

    public int Columns { get { return _cols; } }
    public int Rows { get { return _rows; } }
    public int Count { get { return _cols * _rows; } }
    public float Gap { get { return _gap; } }
    public float Padding { get { return _padding; } }
    public float CellWidth { get { return _cellW; } }
    public float CellHeight { get { return _cellH; } }
    public Rect Outer { get { return _outer; } }

    /// <summary>
    /// Create a fixed rows/columns grid inside the given rect.
    /// </summary>
    public static D2Grid Simple(Rect outer, int cols, int rows, float gap = 0f, float padding = 0f)
    {
        return new D2Grid(outer, cols, rows, gap, padding);
    }

    /// <summary>
    /// Create a grid with a fixed row count and as many columns as will fit a minimum cell size.
    /// </summary>
    public static D2Grid FitColumns(Rect outer, int rows, float minCellWidth, float gap = 0f, float padding = 0f)
    {
        float innerW = Mathf.Max(0f, outer.width - (Mathf.Max(0f, padding) * 2f));
        int cols = D2LayoutHelpers.ComputeGridColumns(innerW, Mathf.Max(1f, minCellWidth), gap);
        return new D2Grid(outer, cols, rows, gap, padding);
    }

    /// <summary>
    /// Get a cell by zero-based row/column.
    /// </summary>
    public Rect Cell(int row, int column)
    {
        row = Mathf.Clamp(row, 0, _rows - 1);
        column = Mathf.Clamp(column, 0, _cols - 1);

        float x = _outer.x + _padding + ((_cellW + _gap) * column);
        float y = _outer.y + _padding + ((_cellH + _gap) * row);
        return new Rect(x, y, _cellW, _cellH);
    }

    /// <summary>
    /// Get a cell by flat index in row-major order.
    /// </summary>
    public Rect Cell(int index)
    {
        if (index < 0) index = 0;
        int row = index / _cols;
        int col = index % _cols;
        if (row >= _rows)
        {
            row = _rows - 1;
            col = _cols - 1;
        }

        return Cell(row, col);
    }
}
