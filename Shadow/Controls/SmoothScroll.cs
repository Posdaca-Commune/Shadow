using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Shadow.Controls;

/// <summary>
/// Enables animated mouse-wheel scrolling for <see cref="ScrollViewer"/> instances.
/// </summary>
public static class SmoothScroll
{
    private const double PixelsPerWheelUnit = 72;
    private const double Smoothing = 0.28;
    private const double MinDistance = 0.5;
    private const int FrameDelayMs = 16;

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "IsEnabled",
            typeof(SmoothScroll),
            defaultValue: false);

    private static readonly ConditionalWeakTable<ScrollViewer, ScrollState> States = new();

    static SmoothScroll()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(ScrollViewer element) => element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(ScrollViewer element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(ScrollViewer scrollViewer, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.GetNewValue<bool>())
        {
            scrollViewer.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnPointerWheelChanged,
                RoutingStrategies.Tunnel);
            return;
        }

        scrollViewer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        if (States.TryGetValue(scrollViewer, out var state))
        {
            state.AnimationVersion++;
            state.IsAnimating = false;
            state.HasTarget = false;
        }
    }

    private static void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.Handled)
        {
            return;
        }

        var maxOffset = GetMaxOffset(scrollViewer);
        if (maxOffset.X <= 0 && maxOffset.Y <= 0)
        {
            return;
        }

        var state = States.GetOrCreateValue(scrollViewer);
        if (!state.HasTarget || !state.IsAnimating)
        {
            state.TargetOffset = scrollViewer.Offset;
            state.HasTarget = true;
        }

        var next = new Vector(
            state.TargetOffset.X - (e.Delta.X * PixelsPerWheelUnit),
            state.TargetOffset.Y - (e.Delta.Y * PixelsPerWheelUnit));

        next = new Vector(
            Math.Clamp(next.X, 0, maxOffset.X),
            Math.Clamp(next.Y, 0, maxOffset.Y));

        if (Vector.Distance(next, scrollViewer.Offset) < MinDistance
            && Vector.Distance(next, state.TargetOffset) < MinDistance)
        {
            return;
        }

        state.TargetOffset = next;
        e.Handled = true;
        _ = RunAnimationAsync(scrollViewer, state);
    }

    private static async Task RunAnimationAsync(ScrollViewer scrollViewer, ScrollState state)
    {
        if (state.IsAnimating)
        {
            return;
        }

        state.IsAnimating = true;
        var version = ++state.AnimationVersion;

        try
        {
            while (version == state.AnimationVersion && GetIsEnabled(scrollViewer))
            {
                var maxOffset = GetMaxOffset(scrollViewer);
                state.TargetOffset = new Vector(
                    Math.Clamp(state.TargetOffset.X, 0, maxOffset.X),
                    Math.Clamp(state.TargetOffset.Y, 0, maxOffset.Y));

                var from = scrollViewer.Offset;
                var to = state.TargetOffset;
                var dx = to.X - from.X;
                var dy = to.Y - from.Y;

                if (Math.Abs(dx) < MinDistance && Math.Abs(dy) < MinDistance)
                {
                    scrollViewer.Offset = to;
                    break;
                }

                scrollViewer.Offset = new Vector(
                    from.X + (dx * Smoothing),
                    from.Y + (dy * Smoothing));

                await Task.Delay(FrameDelayMs).ConfigureAwait(true);
            }
        }
        finally
        {
            if (version == state.AnimationVersion)
            {
                state.IsAnimating = false;
                state.HasTarget = false;
            }
        }
    }

    private static Vector GetMaxOffset(ScrollViewer scrollViewer)
    {
        var extent = scrollViewer.Extent;
        var viewport = scrollViewer.Viewport;
        return new Vector(
            Math.Max(0, extent.Width - viewport.Width),
            Math.Max(0, extent.Height - viewport.Height));
    }

    private sealed class ScrollState
    {
        public Vector TargetOffset;
        public bool HasTarget;
        public bool IsAnimating;
        public int AnimationVersion;
    }
}
