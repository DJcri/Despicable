using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Reusable filter objects for list/browser/table UIs.
///
/// Filters own their draw logic and match predicate so callers can compose them without bespoke sidebar code.
/// </summary>
public static class D2Filters
{
    public interface IFilter<T>
    {
        string Id { get; }
        string Label { get; }
        bool IsActive { get; }
        float MeasureHeight(UIContext ctx);
        void Draw(UIContext ctx, Rect rect, string label = null);
        bool Matches(T item);
    }

    public abstract class FilterBase<T> : IFilter<T>
    {
        public string Id { get; protected set; }
        public string Label { get; protected set; }
        public string Tooltip { get; protected set; }

        public abstract bool IsActive { get; }

        protected FilterBase(string id, string label, string tooltip = null)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Tooltip = tooltip;
        }

        public virtual float MeasureHeight(UIContext ctx)
        {
            return ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f;
        }

        public abstract void Draw(UIContext ctx, Rect rect, string label = null);
        public abstract bool Matches(T item);
    }

    public sealed class ToggleFilter<T> : FilterBase<T>
    {
        private readonly Func<T, bool> _predicate;

        public bool Enabled;
        public bool Invert;

        public ToggleFilter(string id, string label, Func<T, bool> predicate, bool enabled = false, bool invert = false, string tooltip = null)
            : base(id, label, tooltip)
        {
            _predicate = predicate ?? (_ => true);
            Enabled = enabled;
            Invert = invert;
        }

        public override bool IsActive => Enabled;

        public override void Draw(UIContext ctx, Rect rect, string label = null)
        {
            string localLabel = string.IsNullOrEmpty(label) ? Id : label;
            bool value = Enabled;
            D2Widgets.CheckboxLabeledClipped(ctx, rect, Label, ref value, label: localLabel);
            Enabled = value;
            if (!Tooltip.NullOrEmpty() && ctx != null && ctx.Pass == UIPass.Draw)
                TooltipHandler.TipRegion(rect, Tooltip);
        }

        public override bool Matches(T item)
        {
            if (!Enabled)
                return true;

            bool result = _predicate(item);
            return Invert ? !result : result;
        }
    }

    public class DropdownFilter<T, TValue> : FilterBase<T>
    {
        public sealed class Option
        {
            public string Label;
            public TValue Value;

            public Option(string label, TValue value)
            {
                Label = label ?? string.Empty;
                Value = value;
            }
        }

        private readonly Func<T, TValue> _selector;
        private readonly IEqualityComparer<TValue> _comparer;
        public readonly List<Option> Options = new();
        public int SelectedIndex = -1;

        public DropdownFilter(string id, string label, Func<T, TValue> selector, IEnumerable<Option> options, int selectedIndex = -1, IEqualityComparer<TValue> comparer = null, string tooltip = null)
            : base(id, label, tooltip)
        {
            _selector = selector ?? (_ => default(TValue));
            _comparer = comparer ?? EqualityComparer<TValue>.Default;
            if (options != null)
                Options.AddRange(options);
            if (Options.Count > 0)
            {
                if (selectedIndex < -1) selectedIndex = -1;
                if (selectedIndex >= Options.Count) selectedIndex = Options.Count - 1;
                SelectedIndex = selectedIndex;
            }
        }

        public override bool IsActive => SelectedIndex >= 0 && SelectedIndex < Options.Count;

        public override void Draw(UIContext ctx, Rect rect, string label = null)
        {
            string buttonLabel = Label;
            if (IsActive)
                buttonLabel = Label + ": " + Options[SelectedIndex].Label;

            if (D2Widgets.ButtonText(ctx, rect, buttonLabel, label ?? Id))
            {
                var opts = new List<FloatMenuOption>();
                opts.Add(new FloatMenuOption("Any", () => SelectedIndex = -1));
                for (int i = 0; i < Options.Count; i++)
                {
                    int local = i;
                    opts.Add(new FloatMenuOption(Options[i].Label, () => SelectedIndex = local));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            if (!Tooltip.NullOrEmpty() && ctx != null && ctx.Pass == UIPass.Draw)
                TooltipHandler.TipRegion(rect, Tooltip);
        }

        public override bool Matches(T item)
        {
            if (!IsActive)
                return true;

            TValue current = _selector(item);
            TValue expected = Options[SelectedIndex].Value;
            return _comparer.Equals(current, expected);
        }
    }

    public sealed class IntRangeFilter<T> : FilterBase<T>
    {
        private readonly Func<T, int> _selector;
        public int MinValue;
        public int MaxValue;
        public int CurrentMin;
        public int CurrentMax;
        public bool Enabled;

        public IntRangeFilter(string id, string label, Func<T, int> selector, int minValue, int maxValue, bool enabled = false, string tooltip = null)
            : base(id, label, tooltip)
        {
            _selector = selector ?? (_ => 0);
            MinValue = minValue;
            MaxValue = Mathf.Max(minValue, maxValue);
            CurrentMin = MinValue;
            CurrentMax = MaxValue;
            Enabled = enabled;
        }

        public override bool IsActive => Enabled && (CurrentMin > MinValue || CurrentMax < MaxValue);

        public override float MeasureHeight(UIContext ctx)
        {
            float row = base.MeasureHeight(ctx);
            float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
            return row * 3f + gap * 2f;
        }

        public override void Draw(UIContext ctx, Rect rect, string label = null)
        {
            string root = string.IsNullOrEmpty(label) ? Id : label;
            float row = ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f;
            float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;

            Rect enabledRect = new(rect.x, rect.y, rect.width, row);
            Rect minRect = new(rect.x, enabledRect.yMax + gap, rect.width, row);
            Rect maxRect = new(rect.x, minRect.yMax + gap, rect.width, row);

            bool enabled = Enabled;
            D2Widgets.CheckboxLabeledClipped(ctx, enabledRect, Label, ref enabled, label: root + "/Enabled");
            Enabled = enabled;

            D2Fields.IntStepper(ctx, minRect, ref CurrentMin, 1, MinValue, CurrentMax, label: root + "/Min");
            D2Fields.IntStepper(ctx, maxRect, ref CurrentMax, 1, CurrentMin, MaxValue, label: root + "/Max");

            if (!Tooltip.NullOrEmpty() && ctx != null && ctx.Pass == UIPass.Draw)
                TooltipHandler.TipRegion(enabledRect, Tooltip);
        }

        public override bool Matches(T item)
        {
            if (!Enabled)
                return true;

            int value = _selector(item);
            return value >= CurrentMin && value <= CurrentMax;
        }
    }

    public sealed class SourceFilter<T> : DropdownFilter<T, string>
    {
        public SourceFilter(string id, string label, Func<T, string> selector, IEnumerable<Option> options, int selectedIndex = -1, string tooltip = null)
            : base(id, label, selector, options, selectedIndex, StringComparer.OrdinalIgnoreCase, tooltip)
        {
        }
    }
}
