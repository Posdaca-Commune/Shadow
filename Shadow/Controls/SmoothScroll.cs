using System;
using System.Runtime.CompilerServices;
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
    // Distance applied per mouse-wheel unit (Avalonia delta is typically +/- 1 per notch).
    private const double PixelsPerWheelUnit = 64;

    // Higher = snappier follow. ~18 keeps motion smooth without feeling laggy.
    private const double FollowSpeed = 18;

    private const double MinDistance = 0.25;
    private const double MaxDeltaSeconds = 0.05;

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
        StopAnimation(scrollViewer);
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

        next = ClampOffset(next, maxOffset);

        if (Vector.Distance(next, scrollViewer.Offset) < MinDistance
            && Vector.Distance(next, state.TargetOffset) < MinDistance)
        {
            return;
        }

        state.TargetOffset = next;
        e.Handled = true;
        StartAnimation(scrollViewer, state);
    }

    private static void StartAnimation(ScrollViewer scrollViewer, ScrollState state)
    {
        if (state.IsAnimating)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(scrollViewer);
        if (topLevel is null)
        {
            scrollViewer.Offset = state.TargetOffset;
            state.HasTarget = false;
            return;
        }

        state.IsAnimating = true;
        var version = ++state.AnimationVersion;
        state.LastFrameTime = null;

        void OnFrame(TimeSpan time)
        {
            if (version != state.AnimationVersion || !GetIsEnabled(scrollViewer))
            {
                if (version == state.AnimationVersion)
                {
                    state.IsAnimating = false;
                    state.HasTarget = false;
                    state.LastFrameTime = null;
                }

                return;
            }

            if (TopLevel.GetTopLevel(scrollViewer) is null)
            {
                state.IsAnimating = false;
                state.HasTarget = false;
                state.LastFrameTime = null;
                return;
            }

            var dt = state.LastFrameTime is { } last
                ? (time - last).TotalSeconds
                : 1.0 / 60.0;

            if (dt <= 0)
            {
                dt = 1.0 / 60.0;
            }
            else if (dt > MaxDeltaSeconds)
            {
                // Avoid large jumps after tab switches / long stalls.
                dt = MaxDeltaSeconds;
            }

            state.LastFrameTime = time;

            var maxOffset = GetMaxOffset(scrollViewer);
            state.TargetOffset = ClampOffset(state.TargetOffset, maxOffset);

            var from = scrollViewer.Offset;
            var to = state.TargetOffset;
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;

            if (Math.Abs(dx) < MinDistance && Math.Abs(dy) < MinDistance)
            {
                scrollViewer.Offset = to;
                state.IsAnimating = false;
                state.HasTarget = false;
                state.LastFrameTime = null;
                return;
            }

            // Frame-rate independent exponential smoothing:
            // offset += (target - offset) * (1 - e^(-speed * dt))
            var t = 1.0 - Math.Exp(-FollowSpeed * dt);
            scrollViewer.Offset = new Vector(
                from.X + (dx * t),
                from.Y + (dy * t));

            topLevel.RequestAnimationFrame(OnFrame);
        }

        topLevel.RequestAnimationFrame(OnFrame);
    }

    private static void StopAnimation(ScrollViewer scrollViewer)
    {
        if (!States.TryGetValue(scrollViewer, out var state))
        {
            return;
        }

        state.AnimationVersion++;
        state.IsAnimating = false;
        state.HasTarget = false;
        state.LastFrameTime = null;
    }

    private static Vector ClampOffset(Vector offset, Vector maxOffset) =>
        new(Math.Clamp(offset.X, 0, maxOffset.X), Math.Clamp(offset.Y, 0, maxOffset.Y));

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
        public TimeSpan? LastFrameTime;
    }
}
