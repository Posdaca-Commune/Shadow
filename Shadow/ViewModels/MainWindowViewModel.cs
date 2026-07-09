using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using Shadow.Plugins;

namespace Shadow.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
        : this(PluginCatalog.LoadDefault())
    {
    }

    internal MainWindowViewModel(PluginCatalog pluginCatalog)
    {
        SettingsPage = new SettingsViewModel(pluginCatalog.SettingsSections);
        NavigationItems = new ObservableCollection<NavigationItemViewModel>(
            new[]
            {
                new NavigationItemViewModel(
                    "Home",
                    "主页",
                    "Shadow 工作站首页",
                    FluentAvalonia.UI.Controls.FASymbol.Home,
                    HomePage),
            }
            .Concat(pluginCatalog.NavigationItems.Select(item => new NavigationItemViewModel(item)))
            .GroupBy(item => item.Key)
            .Select(group => group.First()));

        _pages = NavigationItems.ToDictionary(item => item.Key, item => item.Content);
        _pages["Settings"] = SettingsPage;

        CurrentPage = HomePage;
    }

    private readonly Dictionary<string, object> _pages;

    public HomeViewModel HomePage { get; } = new();

    public SettingsViewModel SettingsPage { get; }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSelected))]
    [NotifyPropertyChangedFor(nameof(IsSettingsSelected))]
    [NotifyPropertyChangedFor(nameof(CurrentPageTitle))]
    private string _selectedPageKey = "Home";

    [ObservableProperty]
    private object _currentPage;

    public bool IsHomeSelected => SelectedPageKey == "Home";

    public bool IsSettingsSelected => SelectedPageKey == "Settings";

    public string CurrentPageTitle => SelectedPageKey switch
    {
        "Settings" => "设置",
        _ => "工作站",
    };

    [RelayCommand]
    private void Navigate(string pageKey)
    {
        SelectedPageKey = pageKey;
        CurrentPage = _pages.TryGetValue(pageKey, out var page) ? page : HomePage;
    }
}
