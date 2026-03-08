using UnityEngine;

namespace Despicable.UIFramework.Layout;
// Guardrail-Reason: Pane allocation keeps heuristics and fallback decisions adjacent while layout remains one algorithmic pass.
public static partial class D2PaneLayout
{
    private static bool TryCollapseToFit(float primary, float gap, PaneSpec[] panes, bool[] visible)
    {
        if (GetMinRequired(gap, panes, visible) <= primary)
            return true;

        while (true)
        {
            int collapseIndex = FindBestCollapseCandidate(panes, visible);
            if (collapseIndex < 0)
                break;

            visible[collapseIndex] = false;
            if (GetMinRequired(gap, panes, visible) <= primary)
                return true;
        }

        return false;
    }

    private static int FindBestCollapseCandidate(PaneSpec[] panes, bool[] visible)
    {
        int best = -1;
        int bestPriority = int.MinValue;
        float bestMin = float.MinValue;

        for (int i = 0; i < panes.Length; i++)
        {
            if (!visible[i] || !panes[i].CanCollapse)
                continue;

            if (best < 0 ||
                panes[i].Priority > bestPriority ||
                (panes[i].Priority == bestPriority && panes[i].Min > bestMin))
            {
                best = i;
                bestPriority = panes[i].Priority;
                bestMin = panes[i].Min;
            }
        }

        return best;
    }

    private static float GetMinRequired(float gap, PaneSpec[] panes, bool[] visible)
    {
        int count = 0;
        float total = 0f;
        for (int i = 0; i < panes.Length; i++)
        {
            if (!visible[i])
                continue;

            total += Mathf.Max(0f, panes[i].Min);
            count++;
        }

        if (count > 1)
            total += gap * (count - 1);

        return total;
    }

    private static void AllocateVisibleAlongAxis(Rect outer, Axis axis, float gap, PaneSpec[] panes, bool[] visible, Rect[] rects)
    {
        int count = CountVisible(visible);
        if (count <= 0)
            return;

        float primary = axis == Axis.Horizontal ? outer.width : outer.height;
        float usable = Mathf.Max(0f, primary - (gap * Mathf.Max(0, count - 1)));

        float[] sizes = new float[panes.Length];
        float totalPref = 0f;
        float totalFlex = 0f;

        for (int i = 0; i < panes.Length; i++)
        {
            if (!visible[i])
                continue;

            float pref = Mathf.Max(panes[i].Min, panes[i].Preferred);
            sizes[i] = pref;
            totalPref += pref;
            totalFlex += panes[i].Flex;
        }

        if (totalPref > usable && totalPref > 0f)
        {
            float shrinkBudget = totalPref - usable;
            float shrinkable = 0f;
            for (int i = 0; i < panes.Length; i++)
            {
                if (!visible[i])
                    continue;

                shrinkable += Mathf.Max(0f, sizes[i] - panes[i].Min);
            }

            if (shrinkable > 0f)
            {
                float shrinkRatio = Mathf.Clamp01(shrinkBudget / shrinkable);
                for (int i = 0; i < panes.Length; i++)
                {
                    if (!visible[i])
                        continue;

                    float delta = Mathf.Max(0f, sizes[i] - panes[i].Min);
                    sizes[i] -= delta * shrinkRatio;
                }
            }

            float forcedTotal = 0f;
            for (int i = 0; i < panes.Length; i++)
            {
                if (!visible[i])
                    continue;

                forcedTotal += Mathf.Max(0f, sizes[i]);
            }

            // If the caller insists on preserving this axis, the declared minimums may still
            // exceed the usable space. Compress proportionally below min so the panes stay in the
            // same layout family instead of unexpectedly flipping orientation.
            if (forcedTotal > usable && forcedTotal > 0f)
            {
                float scale = usable / forcedTotal;
                for (int i = 0; i < panes.Length; i++)
                {
                    if (!visible[i])
                        continue;

                    sizes[i] = Mathf.Max(0f, sizes[i] * scale);
                }
            }
        }
        else if (usable > totalPref)
        {
            float extra = usable - totalPref;
            if (totalFlex > 0f)
            {
                for (int i = 0; i < panes.Length; i++)
                {
                    if (!visible[i] || panes[i].Flex <= 0f)
                        continue;

                    sizes[i] += extra * (panes[i].Flex / totalFlex);
                }
            }
            else
            {
                int last = LastVisible(visible);
                if (last >= 0)
                    sizes[last] += extra;
            }
        }

        float cursor = axis == Axis.Horizontal ? outer.xMin : outer.yMin;
        int remainingVisible = count;

        for (int i = 0; i < panes.Length; i++)
        {
            if (!visible[i])
            {
                rects[i] = EmptyAtEdge(outer, axis, cursor);
                continue;
            }

            float size = Mathf.Max(0f, sizes[i]);
            remainingVisible--;

            if (remainingVisible == 0)
            {
                float edge = axis == Axis.Horizontal ? outer.xMax : outer.yMax;
                size = Mathf.Max(0f, edge - cursor);
            }

            if (axis == Axis.Horizontal)
            {
                rects[i] = new Rect(cursor, outer.yMin, size, outer.height);
                cursor = rects[i].xMax;
            }
            else
            {
                rects[i] = new Rect(outer.xMin, cursor, outer.width, size);
                cursor = rects[i].yMax;
            }

            if (remainingVisible > 0)
                cursor += gap;
        }
    }

    private static void AllocateVisibleForcedAlongAxis(Rect outer, Axis axis, float gap, PaneSpec[] panes, Rect[] rects)
    {
        if (panes == null || panes.Length == 0)
            return;

        bool[] visible = new bool[panes.Length];
        for (int i = 0; i < visible.Length; i++)
            visible[i] = true;

        AllocateVisibleAlongAxis(outer, axis, gap, panes, visible, rects);
    }

    private static void AllocateVisibleStacked(Rect outer, Axis axis, float gap, PaneSpec[] panes, Rect[] rects)
    {
        int count = panes.Length;
        if (count <= 0)
            return;

        float primary = axis == Axis.Horizontal ? outer.width : outer.height;
        float usable = Mathf.Max(0f, primary - (gap * Mathf.Max(0, count - 1)));
        float each = count > 0 ? usable / count : 0f;
        float cursor = axis == Axis.Horizontal ? outer.xMin : outer.yMin;

        for (int i = 0; i < panes.Length; i++)
        {
            float size = i == panes.Length - 1
                ? Mathf.Max(0f, (axis == Axis.Horizontal ? outer.xMax : outer.yMax) - cursor)
                : Mathf.Max(0f, each);

            if (axis == Axis.Horizontal)
            {
                rects[i] = new Rect(cursor, outer.yMin, size, outer.height);
                cursor = rects[i].xMax;
            }
            else
            {
                rects[i] = new Rect(outer.xMin, cursor, outer.width, size);
                cursor = rects[i].yMax;
            }

            if (i < panes.Length - 1)
                cursor += gap;
        }
    }

    private static int SelectTabWinner(PaneSpec[] panes, bool[] visible)
    {
        int winner = -1;
        int bestPriority = int.MaxValue;
        float bestPreferred = float.MinValue;

        for (int i = 0; i < panes.Length; i++)
        {
            if (!visible[i])
                continue;

            if (winner < 0 ||
                panes[i].Priority < bestPriority ||
                (panes[i].Priority == bestPriority && panes[i].Preferred > bestPreferred))
            {
                winner = i;
                bestPriority = panes[i].Priority;
                bestPreferred = panes[i].Preferred;
            }
        }

        return winner < 0 ? 0 : winner;
    }

    private static Rect[] NewEmptyRects(int count, Rect outer)
    {
        Rect[] rects = new Rect[count];
        for (int i = 0; i < count; i++)
            rects[i] = new Rect(outer.xMax, outer.yMax, 0f, 0f);
        return rects;
    }

    private static Rect EmptyAtEdge(Rect outer, Axis axis, float cursor)
    {
        if (axis == Axis.Horizontal)
            return new Rect(cursor, outer.yMin, 0f, outer.height);

        return new Rect(outer.xMin, cursor, outer.width, 0f);
    }

    private static int CountVisible(bool[] visible)
    {
        int count = 0;
        for (int i = 0; i < visible.Length; i++)
        {
            if (visible[i])
                count++;
        }

        return count;
    }

    private static int LastVisible(bool[] visible)
    {
        for (int i = visible.Length - 1; i >= 0; i--)
        {
            if (visible[i])
                return i;
        }

        return -1;
    }

    private static int[] CollectVisible(bool[] visible)
    {
        int count = CountVisible(visible);
        int[] indices = new int[count];
        int write = 0;
        for (int i = 0; i < visible.Length; i++)
        {
            if (!visible[i])
                continue;

            indices[write++] = i;
        }

        return indices;
    }

    private static void Record(UIContext ctx, Rect[] rects, PaneSpec[] panes, bool[] visible, string label, Axis axis, FallbackMode mode, bool usedFallback)
    {
        if (ctx == null)
            return;

        string root = string.IsNullOrEmpty(label) ? "PaneLayout" : label;
        string meta = "Axis=" + axis + ",Fallback=" + mode + ",UsedFallback=" + (usedFallback ? "1" : "0");
        ctx.RecordRect(BoundsOf(rects), UIRectTag.Group, root, meta);

        for (int i = 0; i < rects.Length; i++)
        {
            string paneMeta = meta +
                ",Visible=" + (visible[i] ? "1" : "0") +
                ",Min=" + panes[i].Min.ToString("0.##") +
                ",Pref=" + panes[i].Preferred.ToString("0.##") +
                ",Flex=" + panes[i].Flex.ToString("0.##");
            ctx.RecordRect(rects[i], UIRectTag.Panel, root + "/" + (panes[i].Id ?? ("Pane[" + i + "]")), paneMeta);
        }
    }

    private static Rect BoundsOf(Rect[] rects)
    {
        if (rects == null || rects.Length == 0)
            return new Rect(0f, 0f, 0f, 0f);

        bool found = false;
        float xMin = 0f;
        float yMin = 0f;
        float xMax = 0f;
        float yMax = 0f;

        for (int i = 0; i < rects.Length; i++)
        {
            Rect r = rects[i];
            if (r.width <= 0f && r.height <= 0f)
                continue;

            if (!found)
            {
                xMin = r.xMin;
                yMin = r.yMin;
                xMax = r.xMax;
                yMax = r.yMax;
                found = true;
            }
            else
            {
                xMin = Mathf.Min(xMin, r.xMin);
                yMin = Mathf.Min(yMin, r.yMin);
                xMax = Mathf.Max(xMax, r.xMax);
                yMax = Mathf.Max(yMax, r.yMax);
            }
        }

        if (!found)
            return new Rect(0f, 0f, 0f, 0f);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}
