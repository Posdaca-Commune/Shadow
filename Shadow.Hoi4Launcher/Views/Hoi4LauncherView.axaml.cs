using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Shadow.Hoi4Launcher.Models;
using Shadow.Hoi4Launcher.ViewModels;

namespace Shadow.Hoi4Launcher.Views;

public partial class Hoi4LauncherView : UserControl
{
    private Point _dragStartPoint;
    private Border? _draggedCard;
    private PlaysetModEntry? _draggedMod;
    private bool _isDraggingMod;
    private double _draggedCardOriginalOpacity = 1;
    private int _draggedCardOriginalZIndex;
    private TranslateTransform? _draggedCardTransform;
    private int _draggedStartIndex = -1;
    private int _previewInsertIndex = -1;
    private readonly Dictionary<Border, TranslateTransform> _displacedCardTransforms = [];
    private List<PlaysetModCardInfo> _dragStartCards = [];

    public Hoi4LauncherView()
    {
        InitializeComponent();
    }

    private async void CreatePlaysetButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not Hoi4LauncherViewModel viewModel)
        {
            return;
        }

        var nameBox = new TextBox
        {
            PlaceholderText = "播放集名称",
            MinWidth = 320,
        };

        var dialog = new FAContentDialog
        {
            Title = "新增播放集",
            Content = nameBox,
            PrimaryButtonText = "创建",
            CloseButtonText = "取消",
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
            Title = "添加 Mod 到播放集",
            Content = modList,
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
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
        _dragStartCards = GetOrderedPlaysetModCards();
        _draggedCardTransform = new TranslateTransform();
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

    private void PlaysetModCard_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);

        if (_isDraggingMod is false
            || _draggedMod is null
            || DataContext is not Hoi4LauncherViewModel viewModel)
        {
            ResetDragState();
            return;
        }

        MoveDraggedModToPreviewIndex(viewModel);
        ResetDragState();
        e.Handled = true;
    }

    private void MoveDraggedModToPreviewIndex(Hoi4LauncherViewModel viewModel)
    {
        if (_draggedMod is null || _previewInsertIndex < 0)
        {
            return;
        }

        viewModel.MovePlaysetMod(_draggedMod, _previewInsertIndex);
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
            if (_displacedCardTransforms.Remove(card, out var existingTransform))
            {
                existingTransform.Y = 0;
            }

            return;
        }

        if (!_displacedCardTransforms.TryGetValue(card, out var transform))
        {
            transform = new TranslateTransform();
            transform.Transitions = CreateTransformTransitions();
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
            PlaysetInsertIndicator.IsVisible = false;
            return;
        }

        if (PlaysetInsertIndicator.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            transform.Transitions = CreateTransformTransitions();
            PlaysetInsertIndicator.RenderTransform = transform;
        }

        transform.Y = point.Value.Y;
        PlaysetInsertIndicator.Opacity = 1;
        PlaysetInsertIndicator.IsVisible = true;
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

    private static Transitions CreateTransformTransitions()
    {
        return
        [
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TimeSpan.FromMilliseconds(120),
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
        PlaysetInsertIndicator.IsVisible = false;
    }

    private static Border? FindPlaysetModCard(Visual? visual)
    {
        while (visual is not null)
        {
            if (visual is Border border && IsPlaysetModCard(border))
            {
                return border;
            }

            visual = visual.GetVisualParent();
        }

        return null;
    }

    private static bool IsPlaysetModCard(Border border)
    {
        return border.Tag is PlaysetModEntry;
    }

    private static T? FindDataContext<T>(StyledElement? element)
        where T : class
    {
        while (element is not null)
        {
            if (element.DataContext is T typedDataContext)
            {
                return typedDataContext;
            }

            element = element.GetLogicalParent() as StyledElement;
        }

        return null;
    }

    private void ResetDragState()
    {
        if (_draggedCard is not null)
        {
            _draggedCard.RenderTransform = null;
            _draggedCard.Opacity = _draggedCardOriginalOpacity;
            _draggedCard.ZIndex = _draggedCardOriginalZIndex;
            _draggedCard.BoxShadow = default;
        }

        ClearDragPreview();
        _draggedCard = null;
        _draggedCardTransform = null;
        _draggedMod = null;
        _isDraggingMod = false;
        _draggedStartIndex = -1;
        _previewInsertIndex = -1;
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
}
