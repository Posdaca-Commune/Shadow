using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using FluentAvalonia.UI.Controls;
using Shadow.Hoi4Launcher.Models;
using Shadow.Hoi4Launcher.ViewModels;

namespace Shadow.Hoi4Launcher.Views;

public partial class Hoi4LauncherView : UserControl
{
    private Point _dragStartPoint;
    private PlaysetModEntry? _draggedMod;
    private bool _isDraggingMod;

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

    private void PlaysetModsList_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsInteractiveSource(e.Source as StyledElement))
        {
            _draggedMod = null;
            _isDraggingMod = false;
            return;
        }

        _dragStartPoint = e.GetPosition(PlaysetModsList);
        _draggedMod = FindDataContext<PlaysetModEntry>(e.Source as StyledElement);
        _isDraggingMod = false;
    }

    private void PlaysetModsList_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedMod is null || e.GetCurrentPoint(PlaysetModsList).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        var position = e.GetPosition(PlaysetModsList);
        if (Math.Abs(position.X - _dragStartPoint.X) < 4 && Math.Abs(position.Y - _dragStartPoint.Y) < 4)
        {
            return;
        }

        _isDraggingMod = true;
    }

    private void PlaysetModsList_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDraggingMod is false
            || _draggedMod is null
            || DataContext is not Hoi4LauncherViewModel viewModel)
        {
            _draggedMod = null;
            _isDraggingMod = false;
            return;
        }

        var targetMod = FindDataContext<PlaysetModEntry>(e.Source as StyledElement);
        if (targetMod is null || ReferenceEquals(targetMod, _draggedMod))
        {
            _draggedMod = null;
            _isDraggingMod = false;
            return;
        }

        var targetIndex = viewModel.PlaysetMods.IndexOf(targetMod);
        viewModel.MovePlaysetMod(_draggedMod, targetIndex);
        _draggedMod = null;
        _isDraggingMod = false;
        e.Handled = true;
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
