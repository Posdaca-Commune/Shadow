using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Shadow.ParadoxGameLauncher.Localization;
using Shadow.ParadoxGameLauncher.ViewModels;

namespace Shadow.ParadoxGameLauncher.Views;

public partial class ParadoxModsSectionView : UserControl
{
    public ParadoxModsSectionView()
    {
        InitializeComponent();
    }

    private async void ImportModButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ParadoxGameLauncherViewModel viewModel)
        {
            return;
        }
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            return;
        }
        var folder = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = ParadoxGameLauncherStrings.Get("Paradox.Dialog.ImportModTitle"),
            AllowMultiple = false,
        });
        var selected = folder.FirstOrDefault();
        if (selected is null)
        {
            return;
        }
        var path = selected.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        viewModel.ImportModFromFolder(path);
    }
}