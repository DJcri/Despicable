using UnityEngine;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Small "policy" helpers for responsive-ish decisions.
///
/// This is intentionally NOT a layout engine. It exists to answer questions like:
/// "Is this area too cramped to show A and B at the same time? If so, switch to tabs/pages."
///
/// Typical use:
///   bool useTabs = D2LayoutPolicy.ShouldTabListDetails(rect.height, minListH, minDetailsH, ctx.Style.Gap);
/// </summary>
public static class D2LayoutPolicy
{
    public enum StackMode
    {
        Stack,
        Tabs
    }

    /// <summary>
    /// Decide whether two vertical sections should be shown stacked, or placed behind tabs.
    ///
    /// If there is insufficient height for both minimums (plus a gap), return Tabs.
    /// Otherwise return Stack.
    /// </summary>
    public static StackMode ChooseStackOrTabs(float availableHeight, float minAHeight, float minBHeight, float gap)
    {
        float a = Mathf.Max(0f, minAHeight);
        float b = Mathf.Max(0f, minBHeight);
        float g = Mathf.Max(0f, gap);

        // If we can't satisfy both minimums, avoid squeezing and page it.
        if (availableHeight < (a + g + b))
            return StackMode.Tabs;

        return StackMode.Stack;
    }

    /// <summary>
    /// Helper for the common "List + Details" block.
    /// Returns true if the stack is too cramped and should be paged via tabs.
    /// </summary>
    public static bool ShouldTabListDetails(float availableHeight, float minListHeight, float minDetailsHeight, float gap)
    {
        return ChooseStackOrTabs(availableHeight, minListHeight, minDetailsHeight, gap) == StackMode.Tabs;
    }
}
