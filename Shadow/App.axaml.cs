using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Shadow.Plugins;
using Shadow.ViewModels;
using Shadow.Views;

namespace Shadow;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var pluginCatalog = PluginCatalog.LoadDefault();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(pluginCatalog),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
