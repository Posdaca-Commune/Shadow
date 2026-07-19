using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
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
    private readonly record struct PlaysetModCardInfo(Border Card, PlaysetModEntry Mod, double Top, double Stride);

    private const int DragReorderAnimationMs = 150;
    private Point _dragStartPoint;
    private Border? _draggedCard;
    private PlaysetModEntry? _draggedMod;
    private bool _isDraggingMod;
    private double _draggedCardOriginalOpacity = 1;
    private int _draggedCardOriginalZIndex;
    private Control? _draggedItemContainer;
    private int _draggedItemContainerOriginalZIndex;
    private bool _draggedItemContainerOriginalClipToBounds;
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

        viewModel.AvailableModSearchText = string.Empty;
        viewModel.RefreshAvailableModFilters();

        var searchBox = new TextBox
        {
            PlaceholderText = ParadoxGameLauncherStrings.Get("Paradox.Mods.Search.Placeholder"),
            MinHeight = 36,
        };
        searchBox.Bind(
            TextBox.TextProperty,
            new Binding(nameof(ParadoxGameLauncherViewModel.AvailableModSearchText))
            {
                Source = viewModel,
                Mode = BindingMode.TwoWay,
            });

        var modsList = new ListBox
        {
            ItemsSource = viewModel.FilteredAvailableMods,
            ItemTemplate = CreateAvailableModCardTemplate(viewModel),
            ItemContainerTheme = (ControlTheme)Resources["PlainModCardListItemTheme"],
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            MinHeight = 420,
            MaxHeight = 520,
            [ScrollViewer.HorizontalScrollBarVisibilityProperty] = ScrollBarVisibility.Disabled,
            [ScrollViewer.VerticalScrollBarVisibilityProperty] = ScrollBarVisibility.Auto,
        };
        modsList.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());

        // Keep content within a normal FAContentDialog width so the dialog overlay/smoke
        // layer stays full-window and the dialog frame matches "New playset".
        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 10,
            MinWidth = 640,
            Width = 680,
            MaxWidth = 720,
        };
        Grid.SetRow(searchBox, 0);
        Grid.SetRow(modsList, 1);
        content.Children.Add(searchBox);
        content.Children.Add(modsList);

        var dialog = new FAContentDialog
        {
            Title = ParadoxGameLauncherStrings.Get("Paradox.Dialog.AddModTitle"),
            Content = content,
            PrimaryButtonText = ParadoxGameLauncherStrings.Get("Paradox.Dialog.Add"),
            CloseButtonText = ParadoxGameLauncherStrings.Get("Paradox.Dialog.Cancel"),
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        // Slightly wider than default (~548) for mod cards, but not full-window.
        dialog.Resources["ContentDialogMaxWidth"] = 720d;
        dialog.Resources["ContentDialogMinWidth"] = 640d;
        dialog.Resources["ContentDialogMaxHeight"] = 760d;

        ModEntry? selectedMod = null;
        void SelectMod(ModEntry mod, Border card)
        {
            selectedMod = mod;
            dialog.IsPrimaryButtonEnabled = true;
            HighlightSelectedAvailableModCard(modsList, card);
        }

        modsList.AddHandler(
            InputElement.PointerPressedEvent,
            (_, args) =>
            {
                if (args.Source is not Control source || IsInteractiveSource(source))
                {
                    return;
                }

                var card = FindAncestorModCard(source);
                if (card?.DataContext is not ModEntry mod)
                {
                    return;
                }

                SelectMod(mod, card);
                args.Handled = args.ClickCount >= 2;
            },
            RoutingStrategies.Tunnel);

        if (await dialog.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary
            && selectedMod is not null)
        {
            viewModel.AddModToPlayset(selectedMod);
        }

        viewModel.AvailableModSearchText = string.Empty;
    }

    private static IDataTemplate CreateAvailableModCardTemplate(ParadoxGameLauncherViewModel viewModel)
    {
        return new FuncDataTemplate<ModEntry>((mod, _) =>
        {
            // VirtualizingStackPanel temporarily clears a recycled container's content.
            // FuncDataTemplate can therefore be invoked with null despite its generic type.
            if (mod is null)
            {
                return new Border
                {
                    Height = 0,
                    IsVisible = false,
                };
            }

            var root = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#0DFFFFFF")),
                BorderBrush = new SolidColorBrush(Color.Parse("#1AFFFFFF")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
                DataContext = mod,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("92,*,Auto"),
                ColumnSpacing = 12,
                MinHeight = 76,
            };

            var coverBorder = new Border
            {
                Width = 92,
                Height = 58,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#14FFFFFF")),
                ClipToBounds = true,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };

            var coverGrid = new Grid();
            coverGrid.Children.Add(new Image
            {
                Stretch = Stretch.UniformToFill,
                IsVisible = mod.HasCoverImage,
                Source = mod.CoverImage,
            });
            coverGrid.Children.Add(new FASymbolIcon
            {
                Symbol = FASymbol.Image,
                FontSize = 24,
                Opacity = 0.54,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                IsVisible = mod.IsCoverPlaceholderVisible,
            });
            coverBorder.Child = coverGrid;

            var textPanel = new StackPanel
            {
                Spacing = 4,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            };
            textPanel.Children.Add(new TextBlock
            {
                Text = mod.Title,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            });

            var metaPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 6,
            };
            metaPanel.Children.Add(CreateMetaBadge(mod.SourceLabel));
            metaPanel.Children.Add(CreateMetaBadge(mod.VersionLabel));
            textPanel.Children.Add(metaPanel);
            textPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(mod.ContentPath) ? mod.Subtitle : mod.ContentPath,
                FontSize = 11,
                Opacity = 0.62,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            });

            var actions = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                MinWidth = 84,
            };

            var openLocationButton = new Button
            {
                Width = 34,
                Height = 34,
                Padding = new Thickness(0),
                Content = new FASymbolIcon { Symbol = FASymbol.OpenFolder },
            };
            openLocationButton.Click += (_, args) =>
            {
                viewModel.OpenModLocationCommand.Execute(mod);
                args.Handled = true;
            };
            actions.Children.Add(openLocationButton);

            if (mod.CanOpenWorkshopPage)
            {
                var workshopButton = new Button
                {
                    Width = 34,
                    Height = 34,
                    Padding = new Thickness(0),
                    Content = new FASymbolIcon { Symbol = FASymbol.Globe },
                };
                workshopButton.Click += (_, args) =>
                {
                    viewModel.OpenWorkshopPageCommand.Execute(mod);
                    args.Handled = true;
                };
                actions.Children.Add(workshopButton);
            }

            Grid.SetColumn(textPanel, 1);
            Grid.SetColumn(actions, 2);
            // Keep text in the flexible middle column so long titles/paths don't crush the action buttons.
            textPanel.SetValue(Grid.IsSharedSizeScopeProperty, false);
            grid.Children.Add(coverBorder);
            grid.Children.Add(textPanel);
            grid.Children.Add(actions);
            root.Child = grid;
            return root;
        }, supportsRecycling: false);
    }

    private static Border CreateMetaBadge(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#14FFFFFF")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 1, 5, 1),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
            },
        };
    }

    private static Border? FindAncestorModCard(Control? control)
    {
        StyledElement? current = control;
        while (current is not null)
        {
            if (current is Border border && border.DataContext is ModEntry)
            {
                return border;
            }

            current = current.GetLogicalParent() as StyledElement;
        }

        return null;
    }

    private static void HighlightSelectedAvailableModCard(Control listHost, Border selectedCard)
    {
        foreach (var border in listHost.GetVisualDescendants().OfType<Border>())
        {
            if (border.DataContext is not ModEntry)
            {
                continue;
            }

            var isSelected = ReferenceEquals(border, selectedCard);
            border.BorderBrush = new SolidColorBrush(Color.Parse(isSelected ? "#FF60A5FA" : "#1AFFFFFF"));
            border.BorderThickness = new Thickness(isSelected ? 2 : 1);
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
            _draggedItemContainer = card.FindAncestorOfType<ListBoxItem>();
            if (_draggedItemContainer is not null)
            {
                _draggedItemContainerOriginalZIndex = _draggedItemContainer.ZIndex;
                _draggedItemContainerOriginalClipToBounds = _draggedItemContainer.ClipToBounds;
                _draggedItemContainer.ClipToBounds = false;
                _draggedItemContainer.ZIndex = 100;
            }
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

    private void PlaysetModCard_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
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
        var orderedVisibleMods = BuildReorderedVisibleMods(draggedMod, _previewInsertIndex);
        e.Handled = true;

        // Commit order immediately. Rebuilding the bound collection recreates containers,
        // so skip the old drop animation and just clear drag visuals.
        if (orderedVisibleMods.Count > 0)
        {
            viewModel.ReorderPlaysetMod(draggedMod, orderedVisibleMods);
        }

        ResetDragState();
    }

    private IReadOnlyList<PlaysetModEntry> BuildReorderedVisibleMods(PlaysetModEntry draggedMod, int insertIndex)
    {
        var remaining = _dragStartCards
            .Where(item => !ReferenceEquals(item.Mod, draggedMod))
            .Select(item => item.Mod)
            .ToList();

        if (remaining.Count == 0 && _dragStartCards.Count == 1)
        {
            return [draggedMod];
        }

        if (remaining.Count == 0)
        {
            return Array.Empty<PlaysetModEntry>();
        }

        var boundedInsertIndex = Math.Clamp(insertIndex, 0, remaining.Count);
        remaining.Insert(boundedInsertIndex, draggedMod);
        return remaining;
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
            double centerY = cards[index].Top + cards[index].Stride / 2.0;
            if (pointerY < centerY)
            {
                insertIndex = index;
                break;
            }
        }
        // insertIndex is the gap among the remaining cards (0..cards.Count).
        _previewInsertIndex = Math.Clamp(insertIndex, 0, cards.Count);
        ApplyDisplacedCardTransforms(cards, _previewInsertIndex);
        UpdateInsertIndicator(cards, insertIndex);
    }

    private void ApplyDisplacedCardTransforms(IReadOnlyList<PlaysetModCardInfo> cards, int insertIndex)
    {
        if (_draggedStartIndex < 0 || _draggedCard == null)
        {
            return;
        }
        double shift = GetDraggedCardStride();
        for (int compactIndex = 0; compactIndex < cards.Count; compactIndex++)
        {
            PlaysetModCardInfo item = cards[compactIndex];
            int originalIndex = GetCurrentPlaysetIndex(item.Mod);
            double targetOffset = 0;
            if (insertIndex < _draggedStartIndex)
            {
                // Moving upward: open a gap at insertIndex by shifting the middle block down.
                if (originalIndex >= insertIndex && originalIndex < _draggedStartIndex)
                {
                    targetOffset = shift;
                }
            }
            else if (insertIndex > _draggedStartIndex)
            {
                // Moving downward: insertIndex is a gap among remaining cards, which equals the
                // final full-list index of the dragged card after drop.
                if (originalIndex > _draggedStartIndex && originalIndex <= insertIndex)
                {
                    targetOffset = 0.0 - shift;
                }
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
                // Place the indicator in the middle of the trailing item gap.
                targetY = last.Top + last.Stride - 4.0;
            }
            else
            {
                PlaysetModCardInfo previous = cards[compactInsertIndex - 1];
                PlaysetModCardInfo next = cards[compactInsertIndex];
                // Midpoint of the real gap between item slots (includes ListBoxItem margin).
                targetY = (previous.Top + previous.Stride + next.Top) / 2.0 - 1.5;
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
        var cards = PlaysetModsList.GetVisualDescendants()
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

                // Prefer ListBoxItem height+margin so the 8px item spacing is included.
                double stride = GetCardStride(card);
                return new PlaysetModCardInfo(card, mod, top, stride);
            })
            .Where(item => item is not null)
            .Select(item => item!.Value)
            .OrderBy(item => item.Top)
            .ToList();

        // Refine stride from actual adjacent tops when available (most accurate).
        for (var index = 0; index < cards.Count - 1; index++)
        {
            var measured = cards[index + 1].Top - cards[index].Top;
            if (measured > 0.5)
            {
                cards[index] = cards[index] with { Stride = measured };
            }
        }

        if (cards.Count >= 2)
        {
            cards[^1] = cards[^1] with { Stride = cards[^2].Stride };
        }

        return cards;
    }


    private double GetDraggedCardStride()
    {
        if (_draggedMod is not null)
        {
            var start = _dragStartCards.Find(item => ReferenceEquals(item.Mod, _draggedMod));
            if (start.Stride > 0.5)
            {
                return start.Stride;
            }
        }

        if (_draggedCard is not null)
        {
            return GetCardStride(_draggedCard);
        }

        return 0;
    }

    private static double GetCardStride(Border card)
    {
        if (card.FindAncestorOfType<ListBoxItem>() is { } item)
        {
            var height = item.Bounds.Height > 0.5 ? item.Bounds.Height : card.Bounds.Height;
            return height + item.Margin.Top + item.Margin.Bottom;
        }

        return card.Bounds.Height + card.Margin.Top + card.Margin.Bottom;
    }

    private int GetCurrentPlaysetIndex(PlaysetModEntry mod)
    {
        if (DataContext is not ParadoxGameLauncherViewModel viewModel)
        {
            return -1;
        }
        // Prefer the visible filtered list so drag previews stay consistent with on-screen cards.
        var filteredIndex = viewModel.FilteredPlaysetMods.IndexOf(mod);
        return filteredIndex >= 0 ? filteredIndex : viewModel.PlaysetMods.IndexOf(mod);
    }

    private int GetCurrentPlaysetCount()
    {
        if (DataContext is not ParadoxGameLauncherViewModel viewModel)
        {
            return 0;
        }
        return viewModel.FilteredPlaysetMods.Count > 0 || !string.IsNullOrWhiteSpace(viewModel.PlaysetModSearchText)
            ? viewModel.FilteredPlaysetMods.Count
            : viewModel.PlaysetMods.Count;
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

    
    private void RestoreDraggedItemContainer()
    {
        if (_draggedItemContainer is null)
        {
            return;
        }

        _draggedItemContainer.ZIndex = _draggedItemContainerOriginalZIndex;
        _draggedItemContainer.ClipToBounds = _draggedItemContainerOriginalClipToBounds;
        _draggedItemContainer = null;
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
        RestoreDraggedItemContainer();
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
        Control? draggedItemContainer = _draggedItemContainer;
        int draggedItemContainerOriginalZIndex = _draggedItemContainerOriginalZIndex;
        bool draggedItemContainerOriginalClipToBounds = _draggedItemContainerOriginalClipToBounds;
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
        if (draggedItemContainer is not null)
        {
            draggedItemContainer.ZIndex = draggedItemContainerOriginalZIndex;
            draggedItemContainer.ClipToBounds = draggedItemContainerOriginalClipToBounds;
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
        // _previewInsertIndex is the gap among remaining cards. Map it back to a stable
        // visual target using the pre-drag card tops.
        var remaining = _dragStartCards
            .Select((item, index) => (item, index))
            .Where(pair => pair.index != startIndex)
            .Select(pair => pair.item)
            .ToList();
        double targetTop;
        if (remaining.Count == 0)
        {
            targetTop = _dragStartCards[startIndex].Top;
        }
        else if (_previewInsertIndex <= 0)
        {
            targetTop = remaining[0].Top;
        }
        else if (_previewInsertIndex >= remaining.Count)
        {
            var last = remaining[^1];
            targetTop = last.Top + last.Stride;
        }
        else
        {
            targetTop = remaining[_previewInsertIndex].Top;
        }

        return targetTop - _dragStartCards[startIndex].Top;
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
        _draggedItemContainer = null;
        _dragStartCards.Clear();
    }

    private static int ResolvePlaysetMoveTargetIndex(
        ParadoxGameLauncherViewModel viewModel,
        PlaysetModEntry draggedMod,
        int filteredTargetIndex)
    {
        if (string.IsNullOrWhiteSpace(viewModel.PlaysetModSearchText)
            || viewModel.FilteredPlaysetMods.Count == viewModel.PlaysetMods.Count)
        {
            return filteredTargetIndex;
        }
        if (filteredTargetIndex < 0 || viewModel.FilteredPlaysetMods.Count == 0)
        {
            return viewModel.PlaysetMods.IndexOf(draggedMod);
        }
        if (filteredTargetIndex >= viewModel.FilteredPlaysetMods.Count)
        {
            var lastVisible = viewModel.FilteredPlaysetMods[^1];
            var lastIndex = viewModel.PlaysetMods.IndexOf(lastVisible);
            return lastIndex < 0 ? viewModel.PlaysetMods.Count - 1 : lastIndex;
        }
        var targetMod = viewModel.FilteredPlaysetMods[filteredTargetIndex];
        if (ReferenceEquals(targetMod, draggedMod))
        {
            return viewModel.PlaysetMods.IndexOf(draggedMod);
        }
        return viewModel.PlaysetMods.IndexOf(targetMod);
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
