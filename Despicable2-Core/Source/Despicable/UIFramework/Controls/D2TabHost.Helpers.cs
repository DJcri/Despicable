using System.Collections.Generic;
using UnityEngine;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
public static partial class D2TabHost
{
    private static void EnsureValidSelection(List<TabGroupDef> visibleGroups, ref State state)
    {
        if (visibleGroups == null || visibleGroups.Count == 0)
        {
            state.ActiveGroupId = null;
            state.ActiveTabId = null;
            return;
        }

        var activeGroup = ResolveActiveGroup(visibleGroups, state.ActiveGroupId) ?? visibleGroups[0];
        state.ActiveGroupId = activeGroup.Id;

        var visibleTabs = GetVisibleTabs(activeGroup);
        if (visibleTabs.Count == 0)
        {
            state.ActiveTabId = null;
            return;
        }

        var activeTab = ResolveActiveTab(visibleTabs, state.ActiveTabId) ?? visibleTabs[0];
        state.ActiveTabId = activeTab.Id;
    }

    private static string DrawSelectorRow(UIContext ctx, Rect rect, IList<TabGroupDef> groups, string selectedId, string hostLabel, string selectorLabel)
    {
        if (groups == null || groups.Count == 0)
            return selectedId;

        var h = new HRow(ctx, rect);
        float w = rect.width / groups.Count;
        string resolved = selectedId;

        for (int i = 0; i < groups.Count; i++)
        {
            bool isLast = i == groups.Count - 1;
            Rect r = isLast ? h.Remaining() : h.NextFixed(w);
            bool selected = resolved == groups[i].Id;
            var spec = new D2Selectors.SelectorSpec(
                id: hostLabel + "/" + selectorLabel + "[" + i + "]",
                label: groups[i].Label,
                tooltip: null,
                selected: selected,
                disabled: false,
                disabledReason: null);
            if (D2Selectors.SelectorButton(ctx, r, spec))
                resolved = groups[i].Id;
        }

        return resolved;
    }

    private static string DrawSelectorRow(UIContext ctx, Rect rect, IList<TabDef> tabs, string selectedId, string hostLabel, string selectorLabel)
    {
        if (tabs == null || tabs.Count == 0)
            return selectedId;

        var h = new HRow(ctx, rect);
        float w = rect.width / tabs.Count;
        string resolved = selectedId;

        for (int i = 0; i < tabs.Count; i++)
        {
            bool isLast = i == tabs.Count - 1;
            Rect r = isLast ? h.Remaining() : h.NextFixed(w);
            var tab = tabs[i];
            bool selected = resolved == tab.Id;
            var spec = new D2Selectors.SelectorSpec(
                id: hostLabel + "/" + selectorLabel + "[" + i + "]",
                label: tab.Label,
                tooltip: tab.Tooltip,
                selected: selected,
                disabled: tab.Disabled,
                disabledReason: tab.DisabledReason);
            if (D2Selectors.SelectorButton(ctx, r, spec))
                resolved = tab.Id;
        }

        return resolved;
    }

    private static TabGroupDef ResolveActiveGroup(IList<TabGroupDef> groups, string activeGroupId)
    {
        if (groups == null || groups.Count == 0)
            return null;

        if (!string.IsNullOrEmpty(activeGroupId))
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null && groups[i].Id == activeGroupId)
                    return groups[i];
            }
        }

        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null)
                return groups[i];
        }

        return null;
    }

    private static TabDef ResolveActiveTab(IList<TabDef> tabs, string activeTabId)
    {
        if (tabs == null || tabs.Count == 0)
            return null;

        if (!string.IsNullOrEmpty(activeTabId))
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i] != null && tabs[i].Id == activeTabId)
                    return tabs[i];
            }
        }

        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null)
                return tabs[i];
        }

        return null;
    }

    private static int CountVisibleGroups(IList<TabGroupDef> groups)
    {
        int count = 0;
        if (groups == null)
            return 0;

        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null && groups[i].Visible)
                count++;
        }

        return count;
    }

    private static int CountVisibleTabs(TabGroupDef group)
    {
        if (group == null || group.Tabs == null)
            return 0;

        int count = 0;
        for (int i = 0; i < group.Tabs.Count; i++)
        {
            if (group.Tabs[i] != null && group.Tabs[i].Visible)
                count++;
        }

        return count;
    }

    private static List<TabGroupDef> GetVisibleGroups(IList<TabGroupDef> groups)
    {
        var list = new List<TabGroupDef>();
        if (groups == null)
            return list;

        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (group != null && group.Visible)
                list.Add(group);
        }

        return list;
    }

    private static List<TabDef> GetVisibleTabs(TabGroupDef group)
    {
        var list = new List<TabDef>();
        if (group == null || group.Tabs == null)
            return list;

        for (int i = 0; i < group.Tabs.Count; i++)
        {
            var tab = group.Tabs[i];
            if (tab != null && tab.Visible)
                list.Add(tab);
        }

        return list;
    }
}
