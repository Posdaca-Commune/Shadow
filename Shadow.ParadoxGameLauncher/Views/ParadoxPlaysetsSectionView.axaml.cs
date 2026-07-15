using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Shadow.ParadoxGameLauncher.Localization;
using Shadow.ParadoxGameLauncher.Models;
using Shadow.ParadoxGameLauncher.ViewModels;

namespace Shadow.ParadoxGameLauncher.Views;

public partial class ParadoxPlaysetsSectionView : UserControl
{
    private readonly record struct PlaysetModCardInfo(Border Card, PlaysetModEntry Mod, double Top);

    private const int DragReorderAnimationMs = 150;
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
    private readonly Dictionary<Border, TranslateTransform> _displacedCardTransforms = [];
    private List<PlaysetModCardInfo> _dragStartCards = [];

    public ParadoxPlaysetsSectionView()
    {
        InitializeComponent();
    }
    private async void CreatePlaysetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ParadoxGameLauncherViewModel viewModel)
        {
            return;
        }

        var nameBox = new TextBox
        {
            PlaceholderText = ParadoxGameLauncherStrings.Get("Paradox.Dialog.PlaysetName"),
            MinWidth = 320,
        };

        var dialog = new FAContentDialog
        {
            Title = ParadoxGameLauncherStrings.Get("Paradox.Dialog.NewPlaysetTitle"),
            Content = nameBox,
            PrimaryButtonText = ParadoxGameLauncherStrings.Get("Paradox.Dialog.Create"),
            CloseButtonText = ParadoxGameLauncherStrings.Get("Paradox.Dialog.Cancel"),
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

    private async void AddModButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ParadoxGameLauncherViewModel viewModel)
        {
            return;
        }

        var modList = new ListBox
        {
            ItemsSource = viewModel.AvailableMods,
            DisplayMemberBinding = new Binding("Title"),
            MinWidth = 460,
            MinHeight = 360,
            MaxHeight = 520,
        };

        var dialog = new FAContentDialog
        {
            Title = ParadoxGameLauncherStrings.Get("Paradox.Dialog.AddModTitle"),
            Content = modList,
            PrimaryButtonText = ParadoxGameLauncherStrings.Get("Paradox.Dialog.Add"),
            CloseButtonText = ParadoxGameLauncherStrings.Get("Paradox.Dialog.Cancel"),
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        modList.SelectionChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = modList.SelectedItem is ModEntry;
        };

        if (await dialog.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary
            && modList.SelectedItem is ModEntry mod)
        {
            viewModel.AddModToPlayset(mod);
        }
    }

    private void PlaysetModCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsInteractiveSource(e.Source as StyledElement))
        {
            ResetDragState();
        }
        else if (sender is Border { DataContext: PlaysetModEntry mod } card)
        {
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
        else
        {
            ResetDragState();
        }
    }

    private void PlaysetModCard_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedMod == null || _draggedCard == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        Point position = e.GetPosition(this);
        if (!(Math.Abs(position.X - _dragStartPoint.X) < 4.0) || !(Math.Abs(position.Y - _dragStartPoint.Y) < 4.0))
        {
            _isDraggingMod = true;
            _draggedCard.Opacity = 0.86;
            _draggedCard.BoxShadow = BoxShadows.Parse("0 8 18 0 #40000000");
            if (_draggedCardTransform != null)
            {
                _draggedCardTransform.X = position.X - _dragStartPoint.X;
                _draggedCardTransform.Y = position.Y - _dragStartPoint.Y;
            }
            UpdateDragPreview(e);
            e.Handled = true;
        }
    }

    private async void PlaysetModCard_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        if (!_isDraggingMod
            || _draggedMod is null
            || DataContext is not ParadoxGameLauncherViewModel viewModel)
        {
            ResetDragState();
            return;
        }

        PlaysetModEntry draggedMod = _draggedMod;
        int targetIndex = _previewInsertIndex;
        e.Handled = true;
        await ResetDragStateWithAnimationAsync(() =>
        {
            viewModel.MovePlaysetMod(draggedMod, targetIndex);
        });
    }

    private void UpdateDragPreview(PointerEventArgs e)
    {
        if (_draggedCard == null || _draggedMod == null || _draggedStartIndex < 0)
        {
            return;
        }
        List<PlaysetModCardInfo> cards = _dragStartCards.Where(item => item.Mod != _draggedMod).ToList();
        if (cards.Count == 0)
        {
            return;
        }
        double pointerY = e.GetPosition(this).Y;
        int insertIndex = cards.Count;
        for (int index = 0; index < cards.Count; index++)
        {
            double centerY = cards[index].Top + cards[index].Card.Bounds.Height / 2.0;
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
        if (_draggedStartIndex < 0 || _draggedCard == null)
        {
            return;
        }
        double shift = _draggedCard.Bounds.Height + _draggedCard.Margin.Bottom + _draggedCard.Margin.Top;
        for (int compactIndex = 0; compactIndex < cards.Count; compactIndex++)
        {
            PlaysetModCardInfo item = cards[compactIndex];
            int originalIndex = GetCurrentPlaysetIndex(item.Mod);
            double targetOffset = 0;
            if (insertIndex < _draggedStartIndex)
            {
                if (originalIndex >= insertIndex && originalIndex < _draggedStartIndex)
                {
                    targetOffset = shift;
                }
            }
            else if (insertIndex > _draggedStartIndex && originalIndex > _draggedStartIndex && originalIndex <= insertIndex)
            {
                targetOffset = 0.0 - shift;
            }
            SetDisplacedCardOffset(item.Card, targetOffset);
        }
    }

    private void SetDisplacedCardOffset(Border card, double targetOffset)
    {
        if (Math.Abs(targetOffset) < 0.1)
        {
            if (_displacedCardTransforms.TryGetValue(card, out TranslateTransform? existingTransform))
            {
                existingTransform.Y = 0;
            }
            return;
        }
        if (!_displacedCardTransforms.TryGetValue(card, out TranslateTransform? transform))
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
        double targetY = 0;
        if (cards.Count > 0)
        {
            if (compactInsertIndex <= 0)
            {
                targetY = cards[0].Top - 4.0;
            }
            else if (compactInsertIndex >= cards.Count)
            {
                PlaysetModCardInfo last = cards[cards.Count - 1];
                targetY = last.Top + last.Card.Bounds.Height + last.Card.Margin.Bottom / 2.0;
            }
            else
            {
                PlaysetModCardInfo previous = cards[compactInsertIndex - 1];
                PlaysetModCardInfo next = cards[compactInsertIndex];
                targetY = (previous.Top + previous.Card.Bounds.Height + next.Top) / 2.0 - 1.5;
            }
        }
        Point? point = this.TranslatePoint(new Point(0.0, targetY), PlaysetDragSurface);
        if (!point.HasValue)
        {
            HideInsertIndicator();
            return;
        }
        TranslateTransform? transform = PlaysetInsertIndicator.RenderTransform as TranslateTransform;
        if (transform is null)
        {
            transform = new TranslateTransform
            {
                Transitions = CreateYAxisTransformTransitions(),
            };
            PlaysetInsertIndicator.RenderTransform = transform;
        }
        Border playsetInsertIndicator = PlaysetInsertIndicator;
        if (playsetInsertIndicator.Transitions == null)
        {
            playsetInsertIndicator.Transitions ??= CreateIndicatorTransitions();
        }
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
            .Select(card =>
            {
                if (card.Tag is not PlaysetModEntry mod)
                {
                    return (PlaysetModCardInfo?)null;
                }

                double top = card.TranslatePoint(new Point(0.0, 0.0), this)?.Y ?? double.NaN;
                if (double.IsNaN(top))
                {
                    return null;
                }

                return new PlaysetModCardInfo(card, mod, top);
            })
            .Where(item => item is not null)
            .Select(item => item!.Value)
            .OrderBy(item => item.Top)
            .ToList();
    }

    private int GetCurrentPlaysetIndex(PlaysetModEntry mod)
    {
        return (DataContext is ParadoxGameLauncherViewModel viewModel) ? viewModel.PlaysetMods.IndexOf(mod) : (-1);
    }

    private int GetCurrentPlaysetCount()
    {
        return (DataContext is ParadoxGameLauncherViewModel viewModel) ? viewModel.PlaysetMods.Count : 0;
    }

    private static Transitions CreateYAxisTransformTransitions()
    {
        return new Transitions
        {
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            }
        };
    }

    private static Transitions CreateDropTransformTransitions()
    {
        return new Transitions
        {
            new DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            },
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            }
        };
    }

    private static Transitions CreateCardTransitions()
    {
        return new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            },
            new BoxShadowsTransition
            {
                Property = Border.BoxShadowProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            }
        };
    }

    private static Transitions CreateIndicatorTransitions()
    {
        return new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            }
        };
    }

    private void ClearDragPreview()
    {
        foreach (KeyValuePair<Border, TranslateTransform> displacedCardTransform in _displacedCardTransforms)
        {
            displacedCardTransform.Deconstruct(out var key, out var value);
            Border card = key;
            TranslateTransform transform = value;
            transform.Y = 0;
            if (card != _draggedCard)
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
        Border playsetInsertIndicator = PlaysetInsertIndicator;
        if (playsetInsertIndicator.Transitions == null)
        {
            playsetInsertIndicator.Transitions ??= CreateIndicatorTransitions();
        }
        PlaysetInsertIndicator.Opacity = 0;
    }

    private static bool IsPlaysetModCard(Border border)
    {
        return border.Tag is PlaysetModEntry;
    }

    private void ResetDragState()
    {
        if (_draggedCard != null)
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
        Border? draggedCard = _draggedCard;
        TranslateTransform? draggedCardTransform = _draggedCardTransform;
        double draggedCardDropYOffset = GetDraggedCardDropYOffset();
        double draggedCardOriginalOpacity = _draggedCardOriginalOpacity;
        int draggedCardOriginalZIndex = _draggedCardOriginalZIndex;
        Transitions? draggedCardOriginalTransitions = _draggedCardOriginalTransitions;
        BoxShadows draggedCardOriginalBoxShadow = _draggedCardOriginalBoxShadow;
        List<KeyValuePair<Border, TranslateTransform>> displacedTransforms = _displacedCardTransforms.ToList();
        int insertIndicatorVersion = _insertIndicatorAnimationVersion + 1;
        _displacedCardTransforms.Clear();
        ClearDragFields();
        if (draggedCard != null)
        {
            draggedCard.Opacity = draggedCardOriginalOpacity;
            draggedCard.BoxShadow = draggedCardOriginalBoxShadow;
            if (draggedCardTransform != null)
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
            if (card.RenderTransform == transform)
            {
                card.RenderTransform = null;
            }
        }
        if (_insertIndicatorAnimationVersion == insertIndicatorVersion)
        {
            PlaysetInsertIndicator.IsVisible = false;
        }
        if (draggedCard != null && draggedCard.RenderTransform == draggedCardTransform)
        {
            draggedCard.ZIndex = draggedCardOriginalZIndex;
            draggedCard.RenderTransform = null;
            draggedCard.Transitions = draggedCardOriginalTransitions;
        }
    }

    private double GetDraggedCardDropYOffset()
    {
        if (_draggedMod == null || _previewInsertIndex < 0 || _dragStartCards.Count == 0)
        {
            return 0;
        }
        int startIndex = _dragStartCards.FindIndex(item => item.Mod == _draggedMod);
        if (startIndex < 0)
        {
            return 0;
        }
        int targetIndex = Math.Clamp(_previewInsertIndex, 0, _dragStartCards.Count - 1);
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
        while (element != null)
        {
            if (element is Button)
            {
                return true;
            }
            element = element.GetLogicalParent() as StyledElement;
        }
        return false;
    }

}
