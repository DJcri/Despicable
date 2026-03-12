using System;
using System.Collections.Generic;
using UnityEngine;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Framework-native tab host with optional context-switched tab groups.
///
/// Unlike D2Tabs, this owns:
/// - group selection (when multiple groups are visible)
/// - tab selection within the active group
/// - body dispatch for the active tab
///
/// Keep the tabs themselves stateless; persist only State in the caller.
/// </summary>
public static partial class D2TabHost
{
    public sealed class TabDef
    {
        public string Id;
        public string Label;
        public string Tooltip;
        public bool Disabled;
        public string DisabledReason;
        public bool Visible = true;
        public Action<UIContext, Rect> Draw;

        public TabDef(string id, string label, Action<UIContext, Rect> draw = null)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Draw = draw;
        }
    }

    public sealed class TabGroupDef
    {
        public string Id;
        public string Label;
        public bool Visible = true;
        public readonly List<TabDef> Tabs = new();

        public TabGroupDef(string id, string label = null)
        {
            Id = id ?? string.Empty;
            Label = label ?? id ?? string.Empty;
        }
    }

    public struct State
    {
        public string ActiveGroupId;
        public string ActiveTabId;
    }

    public static float MeasureHeaderHeight(UIContext ctx, IList<TabGroupDef> groups)
    {
        if (groups == null || groups.Count == 0)
            return 0f;

        float row = ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f;
        int visibleGroups = CountVisibleGroups(groups);
        int visibleTabs = CountVisibleTabs(ResolveActiveGroup(groups, null));

        float h = 0f;
        if (visibleGroups > 1)
            h += row;
        if (visibleTabs > 0)
        {
            if (h > 0f)
                h += (ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f);
            h += row;
        }

        return h;
    }

    public static TabDef DrawHeader(UIContext ctx, Rect rect, IList<TabGroupDef> groups, ref State state, string label = null)
    {
        if (groups == null || groups.Count == 0)
            return null;

        ctx?.RecordRect(rect, UIRectTag.Input, label ?? "TabHost");

        float row = ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f;
        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;

        var visibleGroups = GetVisibleGroups(groups);
        if (visibleGroups.Count == 0)
            return null;

        EnsureValidSelection(visibleGroups, ref state);

        var activeGroup = ResolveActiveGroup(visibleGroups, state.ActiveGroupId);
        if (activeGroup == null)
            return null;

        var visibleTabs = GetVisibleTabs(activeGroup);
        if (visibleTabs.Count == 0)
            return null;

        EnsureValidSelection(visibleGroups, ref state);
        activeGroup = ResolveActiveGroup(visibleGroups, state.ActiveGroupId);
        visibleTabs = GetVisibleTabs(activeGroup);
        var activeTab = ResolveActiveTab(visibleTabs, state.ActiveTabId);
        if (activeTab == null)
            return null;

        var v = new D2VStack(ctx, rect);

        if (visibleGroups.Count > 1)
        {
            Rect groupRow = v.Next(row, UIRectTag.Input, (label ?? "TabHost") + "/Groups");
            state.ActiveGroupId = DrawSelectorRow(ctx, groupRow, visibleGroups, state.ActiveGroupId, label ?? "TabHost", selectorLabel: "Group");

            activeGroup = ResolveActiveGroup(visibleGroups, state.ActiveGroupId);
            visibleTabs = GetVisibleTabs(activeGroup);
            if (ResolveActiveTab(visibleTabs, state.ActiveTabId) == null && visibleTabs.Count > 0)
                state.ActiveTabId = visibleTabs[0].Id;

            if (visibleTabs.Count > 0)
                v.NextSpace(gap);
        }

        Rect tabRow = v.Next(row, UIRectTag.Input, (label ?? "TabHost") + "/Tabs");
        state.ActiveTabId = DrawSelectorRow(ctx, tabRow, visibleTabs, state.ActiveTabId, label ?? "TabHost", selectorLabel: "Tab");

        return ResolveActiveTab(visibleTabs, state.ActiveTabId);
    }

    public static void DrawBody(UIContext ctx, Rect rect, IList<TabGroupDef> groups, ref State state, string label = null)
    {
        var activeTab = Resolve(ctx, groups, ref state);
        if (activeTab == null)
            return;

        ctx?.RecordRect(rect, UIRectTag.PanelSoft, (label ?? "TabHost") + "/Body");
        activeTab.Draw?.Invoke(ctx, rect);
    }

    public static void Draw(UIContext ctx, Rect rect, IList<TabGroupDef> groups, ref State state, string label = null)
    {
        if (groups == null || groups.Count == 0)
            return;

        float headerH = MeasureHeaderHeight(ctx, groups);
        if (headerH <= 0f)
        {
            DrawBody(ctx, rect, groups, ref state, label ?? "TabHost");
            return;
        }

        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
        D2RectSplit.SplitHorizontal(rect, headerH, gap, out Rect top, out Rect body);
        DrawHeader(ctx, top, groups, ref state, label ?? "TabHost");
        DrawBody(ctx, body, groups, ref state, label ?? "TabHost");
    }

    public static TabDef Resolve(UIContext ctx, IList<TabGroupDef> groups, ref State state)
    {
        if (groups == null || groups.Count == 0)
            return null;

        var visibleGroups = GetVisibleGroups(groups);
        if (visibleGroups.Count == 0)
            return null;

        EnsureValidSelection(visibleGroups, ref state);
        var activeGroup = ResolveActiveGroup(visibleGroups, state.ActiveGroupId);
        if (activeGroup == null)
            return null;

        var visibleTabs = GetVisibleTabs(activeGroup);
        if (visibleTabs.Count == 0)
            return null;

        var activeTab = ResolveActiveTab(visibleTabs, state.ActiveTabId);
        if (activeTab == null)
        {
            activeTab = visibleTabs[0];
            state.ActiveTabId = activeTab.Id;
        }

        return activeTab;
    }

}
