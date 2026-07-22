using System;
using System.Threading.Tasks;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Shadow.Abstractions;
using Shadow.Models;
using Shadow.ViewModels;

namespace Shadow.Views;

public partial class MainWindow : FAAppWindow
{
    private const double TitleBarHeight = 48;
    private const double ExpandedPaneLength = 260;
    private const double CompactPaneLength = 72;
    private const int WindowStateAnimationMs = 120;
    private readonly ScaleTransform _contentSurfaceScale = new(1, 1);
    private bool _isWindowStateAnimationReady;
    private bool _animationsEnabled = true;
    private WindowBackdropKind _currentBackdrop = WindowBackdropKind.Mica;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureTitleBar();
        ConfigureWindowStateAnimation();
        Opened += (_, _) =>
        {
            _isWindowStateAnimationReady = true;
            ApplyPersonalizationFromDataContext();
        };
        DataContextChanged += (_, _) => ApplyPersonalizationFromDataContext();
    }

    public void ApplyBackdrop(WindowBackdropKind backdrop)
    {
        _currentBackdrop = backdrop;
        SettingsViewModel.ApplyBackdropToWindow(this, backdrop);
    }

    public void ApplyCompactSidebar(bool compact)
    {
        NavigationView.OpenPaneLength = compact ? CompactPaneLength : ExpandedPaneLength;
        NavigationView.IsPaneOpen = !compact;
        NavigationView.PaneDisplayMode = compact
            ? FANavigationViewPaneDisplayMode.LeftCompact
            : FANavigationViewPaneDisplayMode.Left;
    }

    public void ApplyAnimationsEnabled(bool enabled)
    {
        _animationsEnabled = enabled;
        ShadowUiPreferences.EnableAnimations = enabled;
        if (enabled)
        {
            ConfigureWindowStateAnimation();
        }
        else
        {
            ContentSurface.Transitions = null;
            _contentSurfaceScale.Transitions = null;
            ContentSurface.Opacity = 1;
            _contentSurfaceScale.ScaleX = 1;
            _contentSurfaceScale.ScaleY = 1;
        }
    }

    private void ApplyPersonalizationFromDataContext()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var personalization = viewModel.SettingsPage.Personalization;
        ApplyBackdrop(personalization.Backdrop);
        ApplyCompactSidebar(personalization.ShowCompactSidebar);
        ApplyAnimationsEnabled(personalization.EnableAnimations);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            PlayWindowStateAnimation();
        }
        else if (change.Property == ActualThemeVariantProperty && _currentBackdrop == WindowBackdropKind.Solid)
        {
            SettingsViewModel.ApplyBackdropToWindow(this, WindowBackdropKind.Solid);
        }
    }

    private void ConfigureTitleBar()
    {
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.Height = TitleBarHeight;
        TitleBar.BackgroundColor = Colors.Transparent;
        TitleBar.InactiveBackgroundColor = Colors.Transparent;
        TitleBar.ButtonBackgroundColor = Colors.Transparent;
        TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        ExtendClientAreaTitleBarHeightHint = TitleBarHeight;
        TemplateSettings.TitleBarHeight = TitleBarHeight;
        TemplateSettings.IsTitleBarContentVisible = false;
    }

    private void ConfigureWindowStateAnimation()
    {
        ContentSurface.RenderTransform = _contentSurfaceScale;
        if (!_animationsEnabled)
        {
            ContentSurface.Transitions = null;
            _contentSurfaceScale.Transitions = null;
            return;
        }

        ContentSurface.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(WindowStateAnimationMs),
                Easing = new CubicEaseOut(),
            },
        ];

        _contentSurfaceScale.Transitions =
        [
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleXProperty,
                Duration = TimeSpan.FromMilliseconds(WindowStateAnimationMs),
                Easing = new CubicEaseOut(),
            },
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleYProperty,
                Duration = TimeSpan.FromMilliseconds(WindowStateAnimationMs),
                Easing = new CubicEaseOut(),
            },
        ];
    }

    private async void PlayWindowStateAnimation()
    {
        if (!_animationsEnabled || !_isWindowStateAnimationReady || WindowState == WindowState.Minimized)
        {
            return;
        }

        ContentSurface.Opacity = 0.92;
        _contentSurfaceScale.ScaleX = 0.992;
        _contentSurfaceScale.ScaleY = 0.992;

        await Task.Delay(24);

        ContentSurface.Opacity = 1;
        _contentSurfaceScale.ScaleX = 1;
        _contentSurfaceScale.ScaleY = 1;
    }

    private void TitleBarDragArea_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void NavigationView_OnSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.IsSettingsSelected)
        {
            viewModel.NavigateCommand.Execute("Settings");
            return;
        }

        if (e.SelectedItem is FANavigationViewItem { Tag: string pageKey })
        {
            viewModel.NavigateCommand.Execute(pageKey);
            return;
        }

        if (e.SelectedItem is NavigationItemViewModel navigationItem)
        {
            viewModel.NavigateCommand.Execute(navigationItem.Key);
        }
    }
}
