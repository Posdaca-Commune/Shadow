using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Shadow.Abstractions;
using Shadow.ParadoxGameLauncher.ViewModels;

namespace Shadow.ParadoxGameLauncher.Views;

public partial class ParadoxGameLauncherView : UserControl
{
    private const double SectionEntranceOffset = 18;
    private const int SectionFadeAnimationMs = 160;
    private const int SectionMoveAnimationMs = 220;
    private readonly TranslateTransform _launcherContentTransform = new();
    private int _sectionAnimationVersion;
    private bool _isSectionAnimationReady;

    public ParadoxGameLauncherView()
    {
        ShadowLocalizer.Instance.PropertyChanged += Localizer_OnPropertyChanged;
        InitializeComponent();
        ConfigureSectionTransitions();
        AttachedToVisualTree += (_, _) =>
        {
            _isSectionAnimationReady = true;
            SubscribeToViewModel();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _isSectionAnimationReady = false;
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

    private void ConfigureSectionTransitions()
    {
        LauncherContentHost.RenderTransform = _launcherContentTransform;
        LauncherContentHost.Transitions = CreateSectionHostTransitions();
        _launcherContentTransform.Transitions = CreateSectionTransformTransitions();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ParadoxGameLauncherViewModel.SelectedSection))
        {
            PlaySectionTransition();
        }
    }

    private async void PlaySectionTransition()
    {
        if (!_isSectionAnimationReady)
        {
            return;
        }

        var version = ++_sectionAnimationVersion;

        LauncherContentHost.Transitions = null;
        _launcherContentTransform.Transitions = null;
        LauncherContentHost.Opacity = 0;
        _launcherContentTransform.Y = SectionEntranceOffset;

        await Task.Delay(16);

        if (!_isSectionAnimationReady || version != _sectionAnimationVersion)
        {
            return;
        }

        LauncherContentHost.Transitions = CreateSectionHostTransitions();
        _launcherContentTransform.Transitions = CreateSectionTransformTransitions();
        LauncherContentHost.Opacity = 1;
        _launcherContentTransform.Y = 0;
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

    private static SplineEasing CreateSectionEasing() => new(0.2, 0.8, 0.2);

    private void UnsubscribeFromViewModel()
    {
        if (DataContext is ParadoxGameLauncherViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    private void SubscribeToViewModel()
    {
        if (DataContext is ParadoxGameLauncherViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }
    }

    private void Localizer_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }
}
