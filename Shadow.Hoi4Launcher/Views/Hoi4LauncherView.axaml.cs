using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Shadow.Hoi4Launcher.Localization;
using Shadow.Hoi4Launcher.Models;
using Shadow.Abstractions;
using Shadow.Hoi4Launcher.ViewModels;

namespace Shadow.Hoi4Launcher.Views;

public partial class Hoi4LauncherView : UserControl
{
    private const double SectionEntranceOffset = 18;
    private const int SectionFadeAnimationMs = 160;
    private const int SectionMoveAnimationMs = 220;
    private const int DragReorderAnimationMs = 150;
    private readonly TranslateTransform _launcherContentTransform = new();
    private Point _dragStartPoint;
    private Border? _draggedCard;
    private PlaysetModEntry? _draggedMod;
    private bool _isDraggingMod;
    private double _draggedCardOriginalOpacity = 1;
    private int _draggedCardOriginalZIndex;
    private Transitions? _draggedCardOriginalTransitions;
    private BoxShadows _draggedCardOriginalBoxShadow;
    private TranslateTransform? _draggedCardTransform;
    private int _draggedStartIndex = -1;
    private int _previewInsertIndex = -1;
    private int _insertIndicatorAnimationVersion;
    private int _sectionAnimationVersion;
    private bool _isSectionAnimationReady;
    private readonly Dictionary<Border, TranslateTransform> _displacedCardTransforms = [];
    private List<PlaysetModCardInfo> _dragStartCards = [];

    public Hoi4LauncherView()
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
        if (e.PropertyName == nameof(Hoi4LauncherViewModel.SelectedSection))
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
        if (DataContext is Hoi4LauncherViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    private void SubscribeToViewModel()
    {
        if (DataContext is Hoi4LauncherViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }
    }

    private async void CreatePlaysetButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not Hoi4LauncherViewModel viewModel)
        {
            return;
        }

        var nameBox = new TextBox
        {
            PlaceholderText = Hoi4LauncherStrings.Get("Hoi4.Dialog.PlaysetName"),
            MinWidth = 320,
        };

        var dialog = new FAContentDialog
        {
            Title = Hoi4LauncherStrings.Get("Hoi4.Dialog.NewPlaysetTitle"),
            Content = nameBox,
            PrimaryButtonText = Hoi4LauncherStrings.Get("Hoi4.Dialog.Create"),
            CloseButtonText = Hoi4LauncherStrings.Get("Hoi4.Dialog.Cancel"),
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };
        nameBox.TextChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
        };

        var result = await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        var name = nameBox.Text?.Trim();
        if (result == FAContentDialogResult.Primary && !string.IsNullOrWhiteSpace(name))
        {
            viewModel.AddPlayset(name);
        }
    }

    private async void AddModButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not Hoi4LauncherViewModel viewModel)
        {
            return;
        }

        var modList = new ListBox
        {
            ItemsSource = viewModel.AvailableMods,
            DisplayMemberBinding = new Avalonia.Data.Binding(nameof(ModEntry.Title)),
            MinWidth = 460,
            MinHeight = 360,
            MaxHeight = 520,
        };

        var dialog = new FAContentDialog
        {
            Title = Hoi4LauncherStrings.Get("Hoi4.Dialog.AddModTitle"),
            Content = modList,
            PrimaryButtonText = Hoi4LauncherStrings.Get("Hoi4.Dialog.Add"),
            CloseButtonText = Hoi4LauncherStrings.Get("Hoi4.Dialog.Cancel"),
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };
        modList.SelectionChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = modList.SelectedItem is ModEntry;
        };

        var result = await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        if (result == FAContentDialogResult.Primary && modList.SelectedItem is ModEntry mod)
        {
            viewModel.AddModToPlayset(mod);
        }
    }

    private void PlaysetModCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsInteractiveSource(e.Source as StyledElement))
        {
            ResetDragState();
            return;
        }

        if (sender is not Border card || card.DataContext is not PlaysetModEntry mod)
        {
            ResetDragState();
            return;
        }

        _dragStartPoint = e.GetPosition(this);
        _draggedCard = card;
        _draggedMod = mod;
        _isDraggingMod = false;
        _draggedStartIndex = GetCurrentPlaysetIndex(mod);
        _previewInsertIndex = _draggedStartIndex;
        _draggedCardOriginalOpacity = card.Opacity;
        _draggedCardOriginalZIndex = card.ZIndex;
        _draggedCardOriginalTransitions = card.Transitions;
        _draggedCardOriginalBoxShadow = card.BoxShadow;
        _dragStartCards = GetOrderedPlaysetModCards();
        _draggedCardTransform = new TranslateTransform();
        card.Transitions = CreateCardTransitions();
        card.RenderTransform = _draggedCardTransform;
        card.ZIndex = 100;
        e.Pointer.Capture(card);
    }

    private void PlaysetModCard_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedMod is null || _draggedCard is null || e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStartPoint.X) < 4 && Math.Abs(position.Y - _dragStartPoint.Y) < 4)
        {
            return;
        }

        _isDraggingMod = true;
        _draggedCard.Opacity = 0.86;
        _draggedCard.BoxShadow = BoxShadows.Parse("0 8 18 0 #40000000");
        if (_draggedCardTransform is not null)
        {
            _draggedCardTransform.X = position.X - _dragStartPoint.X;
            _draggedCardTransform.Y = position.Y - _dragStartPoint.Y;
        }

        UpdateDragPreview(e);
        e.Handled = true;
    }

    private async void PlaysetModCard_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);

        if (_isDraggingMod is false
            || _draggedMod is null
            || DataContext is not Hoi4LauncherViewModel viewModel)
        {
            ResetDragState();
            return;
        }

        var draggedMod = _draggedMod;
        var targetIndex = _previewInsertIndex;
        e.Handled = true;
        await ResetDragStateWithAnimationAsync(() => viewModel.MovePlaysetMod(draggedMod, targetIndex));
    }

    private void UpdateDragPreview(PointerEventArgs e)
    {
        if (_draggedCard is null || _draggedMod is null || _draggedStartIndex < 0)
        {
            return;
        }

        var cards = _dragStartCards
            .Where(item => !ReferenceEquals(item.Mod, _draggedMod))
            .ToList();
        if (cards.Count == 0)
        {
            return;
        }

        var pointerY = e.GetPosition(this).Y;
        var insertIndex = cards.Count;
        for (var index = 0; index < cards.Count; index++)
        {
            var centerY = cards[index].Top + cards[index].Card.Bounds.Height / 2;
            if (pointerY < centerY)
            {
                insertIndex = index;
                break;
            }
        }

        _previewInsertIndex = Math.Clamp(insertIndex, 0, GetCurrentPlaysetCount() - 1);
        ApplyDisplacedCardTransforms(cards, _previewInsertIndex);
        UpdateInsertIndicator(cards, insertIndex);
    }

    private void ApplyDisplacedCardTransforms(IReadOnlyList<PlaysetModCardInfo> cards, int insertIndex)
    {
        if (_draggedStartIndex < 0 || _draggedCard is null)
        {
            return;
        }

        var shift = _draggedCard.Bounds.Height + _draggedCard.Margin.Bottom + _draggedCard.Margin.Top;
        for (var compactIndex = 0; compactIndex < cards.Count; compactIndex++)
        {
            var item = cards[compactIndex];
            var originalIndex = GetCurrentPlaysetIndex(item.Mod);
            var targetOffset = 0d;

            if (insertIndex < _draggedStartIndex)
            {
                if (originalIndex >= insertIndex && originalIndex < _draggedStartIndex)
                {
                    targetOffset = shift;
                }
            }
            else if (insertIndex > _draggedStartIndex)
            {
                if (originalIndex > _draggedStartIndex && originalIndex <= insertIndex)
                {
                    targetOffset = -shift;
                }
            }

            SetDisplacedCardOffset(item.Card, targetOffset);
        }
    }

    private void SetDisplacedCardOffset(Border card, double targetOffset)
    {
        if (Math.Abs(targetOffset) < 0.1)
        {
            if (_displacedCardTransforms.TryGetValue(card, out var existingTransform))
            {
                existingTransform.Y = 0;
            }

            return;
        }

        if (!_displacedCardTransforms.TryGetValue(card, out var transform))
        {
            transform = new TranslateTransform();
            transform.Transitions = CreateYAxisTransformTransitions();
            card.RenderTransform = transform;
            _displacedCardTransforms[card] = transform;
        }

        transform.Y = targetOffset;
    }

    private void UpdateInsertIndicator(IReadOnlyList<PlaysetModCardInfo> cards, int compactInsertIndex)
    {
        var targetY = 0d;
        if (cards.Count > 0)
        {
            if (compactInsertIndex <= 0)
            {
                targetY = cards[0].Top - 4;
            }
            else if (compactInsertIndex >= cards.Count)
            {
                var last = cards[^1];
                targetY = last.Top + last.Card.Bounds.Height + last.Card.Margin.Bottom / 2;
            }
            else
            {
                var previous = cards[compactInsertIndex - 1];
                var next = cards[compactInsertIndex];
                targetY = (previous.Top + previous.Card.Bounds.Height + next.Top) / 2 - 1.5;
            }
        }

        var point = this.TranslatePoint(new Point(0, targetY), PlaysetDragSurface);
        if (point is null)
        {
            HideInsertIndicator();
            return;
        }

        if (PlaysetInsertIndicator.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            transform.Transitions = CreateYAxisTransformTransitions();
            PlaysetInsertIndicator.RenderTransform = transform;
        }

        PlaysetInsertIndicator.Transitions ??= CreateIndicatorTransitions();
        PlaysetInsertIndicator.IsVisible = true;
        _insertIndicatorAnimationVersion++;
        transform.Y = point.Value.Y;
        PlaysetInsertIndicator.Opacity = 1;
    }

    private List<PlaysetModCardInfo> GetOrderedPlaysetModCards()
    {
        return PlaysetModsList.GetVisualDescendants()
            .OfType<Border>()
            .Where(IsPlaysetModCard)
            .Select(card => new PlaysetModCardInfo(
                card,
                (PlaysetModEntry)card.Tag!,
                card.TranslatePoint(new Point(0, 0), this)?.Y ?? double.NaN))
            .Where(item => !double.IsNaN(item.Top))
            .OrderBy(item => item.Top)
            .ToList();
    }

    private int GetCurrentPlaysetIndex(PlaysetModEntry mod)
    {
        return DataContext is Hoi4LauncherViewModel viewModel
            ? viewModel.PlaysetMods.IndexOf(mod)
            : -1;
    }

    private int GetCurrentPlaysetCount()
    {
        return DataContext is Hoi4LauncherViewModel viewModel
            ? viewModel.PlaysetMods.Count
            : 0;
    }

    private static Transitions CreateYAxisTransformTransitions()
    {
        return
        [
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TimeSpan.FromMilliseconds(DragReorderAnimationMs),
                Easing = new CubicEaseOut(),
            },
        ];
    }

    private static Transitions CreateDropTransformTransitions()
    {
        return
        [
            new DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = TimeSpan.FromMilliseconds(DragReorderAnimationMs),
                Easing = new CubicEaseOut(),
            },
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TimeSpan.FromMilliseconds(DragReorderAnimationMs),
                Easing = new CubicEaseOut(),
            },
        ];
    }

    private static Transitions CreateCardTransitions()
    {
        return
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(DragReorderAnimationMs),
                Easing = new CubicEaseOut(),
            },
            new BoxShadowsTransition
            {
                Property = Border.BoxShadowProperty,
                Duration = TimeSpan.FromMilliseconds(DragReorderAnimationMs),
                Easing = new CubicEaseOut(),
            },
        ];
    }

    private static Transitions CreateIndicatorTransitions()
    {
        return
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(DragReorderAnimationMs),
                Easing = new CubicEaseOut(),
            },
        ];
    }

    private readonly record struct PlaysetModCardInfo(Border Card, PlaysetModEntry Mod, double Top);

    private void ClearDragPreview()
    {
        foreach (var (card, transform) in _displacedCardTransforms)
        {
            transform.Y = 0;
            if (!ReferenceEquals(card, _draggedCard))
            {
                card.RenderTransform = null;
            }
        }

        _displacedCardTransforms.Clear();
        HideInsertIndicator(immediate: true);
    }

    private void HideInsertIndicator(bool immediate = false)
    {
        _insertIndicatorAnimationVersion++;
        if (immediate)
        {
            PlaysetInsertIndicator.Opacity = 0;
            PlaysetInsertIndicator.IsVisible = false;
            return;
        }

        PlaysetInsertIndicator.Transitions ??= CreateIndicatorTransitions();
        PlaysetInsertIndicator.Opacity = 0;
    }

    private static bool IsPlaysetModCard(Border border)
    {
        return border.Tag is PlaysetModEntry;
    }

    private void ResetDragState()
    {
        if (_draggedCard is not null)
        {
            _draggedCard.RenderTransform = null;
            _draggedCard.Opacity = _draggedCardOriginalOpacity;
            _draggedCard.ZIndex = _draggedCardOriginalZIndex;
            _draggedCard.BoxShadow = _draggedCardOriginalBoxShadow;
            _draggedCard.Transitions = _draggedCardOriginalTransitions;
        }

        ClearDragPreview();
        ClearDragFields();
    }

    private async Task ResetDragStateWithAnimationAsync(Action? commitAfterAnimation)
    {
        var draggedCard = _draggedCard;
        var draggedCardTransform = _draggedCardTransform;
        var draggedCardDropYOffset = GetDraggedCardDropYOffset();
        var draggedCardOriginalOpacity = _draggedCardOriginalOpacity;
        var draggedCardOriginalZIndex = _draggedCardOriginalZIndex;
        var draggedCardOriginalTransitions = _draggedCardOriginalTransitions;
        var draggedCardOriginalBoxShadow = _draggedCardOriginalBoxShadow;
        var displacedTransforms = _displacedCardTransforms.ToList();
        var insertIndicatorVersion = _insertIndicatorAnimationVersion + 1;

        _displacedCardTransforms.Clear();
        ClearDragFields();

        if (draggedCard is not null)
        {
            draggedCard.Opacity = draggedCardOriginalOpacity;
            draggedCard.BoxShadow = draggedCardOriginalBoxShadow;

            if (draggedCardTransform is not null)
            {
                draggedCardTransform.Transitions = CreateDropTransformTransitions();
                draggedCardTransform.X = 0;
                draggedCardTransform.Y = draggedCardDropYOffset;
            }
        }

        HideInsertIndicator();
        await Task.Delay(TimeSpan.FromMilliseconds(DragReorderAnimationMs));

        commitAfterAnimation?.Invoke();

        foreach (var (card, transform) in displacedTransforms)
        {
            if (ReferenceEquals(card.RenderTransform, transform))
            {
                card.RenderTransform = null;
            }
        }

        if (_insertIndicatorAnimationVersion == insertIndicatorVersion)
        {
            PlaysetInsertIndicator.IsVisible = false;
        }

        if (draggedCard is not null)
        {
            if (ReferenceEquals(draggedCard.RenderTransform, draggedCardTransform))
            {
                draggedCard.ZIndex = draggedCardOriginalZIndex;
                draggedCard.RenderTransform = null;
                draggedCard.Transitions = draggedCardOriginalTransitions;
            }
        }
    }

    private double GetDraggedCardDropYOffset()
    {
        if (_draggedMod is null || _previewInsertIndex < 0 || _dragStartCards.Count == 0)
        {
            return 0;
        }

        var startIndex = _dragStartCards.FindIndex(item => ReferenceEquals(item.Mod, _draggedMod));
        if (startIndex < 0)
        {
            return 0;
        }

        var targetIndex = Math.Clamp(_previewInsertIndex, 0, _dragStartCards.Count - 1);
        return _dragStartCards[targetIndex].Top - _dragStartCards[startIndex].Top;
    }

    private void ClearDragFields()
    {
        _draggedCard = null;
        _draggedCardTransform = null;
        _draggedMod = null;
        _isDraggingMod = false;
        _draggedStartIndex = -1;
        _previewInsertIndex = -1;
        _draggedCardOriginalTransitions = null;
        _dragStartCards.Clear();
    }

    
    private static bool IsInteractiveSource(StyledElement? element)
    {
        while (element is not null)
        {
            if (element is Button or CheckBox or ToggleSwitch)
            {
                return true;
            }

            element = element.GetLogicalParent() as StyledElement;
        }

        return false;
    }

    private void Localizer_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(ShadowLocalizer.CultureName)
            or nameof(ShadowLocalizer.Version)
            or "Item[]"
            or null
            or ""))
        {
            return;
        }

        // Compiled bindings for nested text do not always refresh with the plugin page.
        // Re-assigning DataContext forces the whole plugin view tree to rebind.
        var dataContext = DataContext;
        DataContext = null;
        DataContext = dataContext;
    }
}


