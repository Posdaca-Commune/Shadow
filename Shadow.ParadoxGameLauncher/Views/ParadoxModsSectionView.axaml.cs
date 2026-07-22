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
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = ParadoxGameLauncherStrings.Get("Paradox.Dialog.ImportModTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(ParadoxGameLauncherStrings.Get("Paradox.Dialog.ImportModZipFilter"))
                {
                    Patterns = ["*.zip"],
                },
            ],
        });
        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }
        var path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        viewModel.ImportModFromArchive(path);
    }
}
