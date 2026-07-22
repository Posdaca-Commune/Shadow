using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Shadow.Abstractions;
using Shadow.ViewModels;

namespace Shadow.Views;

public partial class SettingsView : UserControl
{
    private const double SectionEntranceOffset = 18;
    private const int SectionFadeAnimationMs = 160;
    private const int SectionMoveAnimationMs = 220;
    private readonly TranslateTransform _settingsContentTransform = new();
    private bool _isAnimationReady;
    private int _sectionAnimationVersion;

    public SettingsView()
    {
        InitializeComponent();
        ConfigureContentTransitions();
        AttachedToVisualTree += (_, _) =>
        {
            _isAnimationReady = true;
            SubscribeToViewModel();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _isAnimationReady = false;
            _sectionAnimationVersion++;
            UnsubscribeFromViewModel();
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        UnsubscribeFromViewModel();
        base.OnDataContextChanged(e);
        SubscribeToViewModel();
    }

    private void ConfigureContentTransitions()
    {
        SettingsContentPanel.RenderTransform = _settingsContentTransform;
        SettingsContentPanel.Transitions = CreateSectionHostTransitions();
        _settingsContentTransform.Transitions = CreateSectionTransformTransitions();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedSection))
        {
            PlaySectionTransition();
        }
    }

    private async void PlaySectionTransition()
    {
        if (!_isAnimationReady || !ShadowUiPreferences.EnableAnimations)
        {
            return;
        }

        var version = ++_sectionAnimationVersion;

        SettingsContentPanel.Transitions = null;
        _settingsContentTransform.Transitions = null;
        SettingsContentPanel.Opacity = 0;
        _settingsContentTransform.Y = SectionEntranceOffset;

        await Task.Delay(16);

        if (!_isAnimationReady || version != _sectionAnimationVersion)
        {
            return;
        }

        SettingsContentPanel.Transitions = CreateSectionHostTransitions();
        _settingsContentTransform.Transitions = CreateSectionTransformTransitions();
        SettingsContentPanel.Opacity = 1;
        _settingsContentTransform.Y = 0;
    }

    private static Transitions CreateSectionHostTransitions()
    {
        return
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(SectionFadeAnimationMs),
                Easing = CreateSectionEasing(),
            },
        ];
    }

    private static Transitions CreateSectionTransformTransitions()
    {
        return
        [
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TimeSpan.FromMilliseconds(SectionMoveAnimationMs),
                Easing = CreateSectionEasing(),
            },
        ];
    }

    private static SplineEasing CreateSectionEasing()
    {
        return new SplineEasing
        {
            X1 = 0.16,
            Y1 = 1,
            X2 = 0.3,
            Y2 = 1,
        };
    }

    private void UnsubscribeFromViewModel()
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    private void SubscribeToViewModel()
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }
    }
}
