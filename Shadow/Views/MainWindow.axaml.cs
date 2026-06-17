using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Avalonia.Input;
using Avalonia.Media;
using Shadow.ViewModels;

namespace Shadow.Views;

public partial class MainWindow : FAAppWindow
{
    private const double TitleBarHeight = 48;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureTitleBar();
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
