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

    private async void RestoreSaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SaveEntry save }
            || DataContext is not ParadoxGameLauncherViewModel viewModel)
        {
            return;
        }

        var backups = viewModel.GetSaveBackups(save);
        if (backups.Count == 0)
        {
            viewModel.NotifyNoSaveBackups();
            return;
        }

        var backupsList = new ListBox
        {
            MinHeight = 220,
            MaxHeight = 380,
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        };
        backupsList.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new StackPanel());
        foreach (var backup in backups)
        {
            backupsList.Items.Add(backup);
        }

        backupsList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SaveBackupEntry>((backup, _) =>
        {
            var panel = new StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 6) };
            panel.Children.Add(new TextBlock
            {
                Text = backup!.CreatedText,
                FontSize = 14,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
            });
            panel.Children.Add(new TextBlock
            {
                Text = backup.SizeText,
                FontSize = 11,
                Opacity = 0.62,
            });
            return panel;
        });

        var dialog = new FAContentDialog
        {
            Title = ParadoxGameLauncherStrings.Format("Paradox.Dialog.RestoreSaveTitle", save.Name),
            Content = backupsList,
            PrimaryButtonText = ParadoxGameLauncherStrings.Get("Paradox.Action.RestoreSave"),
            CloseButtonText = ParadoxGameLauncherStrings.Get("Paradox.Dialog.Cancel"),
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        backupsList.SelectionChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = backupsList.SelectedItem is SaveBackupEntry;
        };

        if (await dialog.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary
            && backupsList.SelectedItem is SaveBackupEntry selectedBackup)
        {
            viewModel.RestoreSaveBackup(save, selectedBackup);
        }
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
