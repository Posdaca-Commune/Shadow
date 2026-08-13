using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Shadow.ParadoxGameLauncher.Localization;
using Shadow.ParadoxGameLauncher.ViewModels;

namespace Shadow.ParadoxGameLauncher.Views;

public partial class ParadoxGameSettingsSectionView : UserControl
{
    public ParadoxGameSettingsSectionView()
    {
        InitializeComponent();
    }

    private async void BrowseGameDirectory_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = GetSettings();
        if (settings is null)
        {
            return;
        }

        var folder = await PickFolderAsync(
            ParadoxGameLauncherStrings.Get("Paradox.Dialog.SelectGameDirectory"),
            settings.GameExecutablePath);
        if (folder is not null)
        {
            settings.TryApplyGameDirectory(folder);
        }
    }

    private async void BrowseUserDirectory_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = GetSettings();
        if (settings is null)
        {
            return;
        }

        var folder = await PickFolderAsync(
            ParadoxGameLauncherStrings.Get("Paradox.Dialog.SelectUserDirectory"),
            settings.GameUserDirectory);
        if (folder is not null)
        {
            settings.ApplyUserDirectory(folder);
        }
    }

    private async void BrowseWorkshopDirectory_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = GetSettings();
        if (settings is null)
        {
            return;
        }

        var folder = await PickFolderAsync(
            ParadoxGameLauncherStrings.Get("Paradox.Dialog.SelectWorkshopDirectory"),
            settings.WorkshopDirectory);
        if (folder is not null)
        {
            settings.ApplyWorkshopDirectory(folder);
        }
    }

    private ParadoxGameSettingsViewModel? GetSettings()
    {
        return DataContext switch
        {
            ParadoxGameSettingsViewModel settings => settings,
            ParadoxGameLauncherViewModel launcher => launcher.GameSettings,
            _ => null,
        };
    }

    private async Task<string?> PickFolderAsync(string title, string? currentPath)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            return null;
        }

        var startFolder = await TryGetStartFolderAsync(storageProvider, currentPath);
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = startFolder,
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private static async Task<IStorageFolder?> TryGetStartFolderAsync(IStorageProvider storageProvider, string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return null;
        }

        var directory = Directory.Exists(currentPath)
            ? currentPath
            : Path.GetDirectoryName(currentPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        return await storageProvider.TryGetFolderFromPathAsync(directory);
    }
}