using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using Shadow.ParadoxGameLauncher.Localization;
using Shadow.ParadoxGameLauncher.Models;
using Shadow.ParadoxGameLauncher.ViewModels;

namespace Shadow.ParadoxGameLauncher.Views;

public partial class ParadoxSavesSectionView : UserControl
{
    public ParadoxSavesSectionView()
    {
        InitializeComponent();
    }

    private async void DeleteSaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SaveEntry save }
            || DataContext is not ParadoxGameLauncherViewModel viewModel)
        {
            return;
        }

        var dialog = new FAContentDialog
        {
            Title = ParadoxGameLauncherStrings.Get("Paradox.Dialog.DeleteSaveTitle"),
            Content = new TextBlock
            {
                Text = ParadoxGameLauncherStrings.Format("Paradox.Dialog.DeleteSaveMessage", save.Name),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            },
            PrimaryButtonText = ParadoxGameLauncherStrings.Get("Paradox.Action.Delete"),
            CloseButtonText = ParadoxGameLauncherStrings.Get("Paradox.Dialog.Cancel"),
            DefaultButton = FAContentDialogButton.Close,
        };

        if (await dialog.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary)
        {
            viewModel.DeleteSave(save);
        }
    }
}