using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;
using Shadow.Plugins;
using Shadow.Services;

namespace Shadow.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
        : this(PluginCatalog.LoadDefault(), ApplicationSettingsStore.Load())
    {
    }

    internal MainWindowViewModel(PluginCatalog pluginCatalog, ApplicationSettings applicationSettings)
    {
        SettingsPage = new SettingsViewModel(pluginCatalog.SettingsSections, pluginCatalog.Plugins, applicationSettings);
        NavigationItems = new ObservableCollection<NavigationItemViewModel>(
            new[]
            {
                new NavigationItemViewModel(
                    "Home",
                    LocalizedText.Key("Shadow.Nav.Home.Title"),
                    LocalizedText.Key("Shadow.Nav.Home.Description"),
                    FASymbol.Home,
                    HomePage),
            }
            .Concat(pluginCatalog.NavigationItems.Select(item => new NavigationItemViewModel(item)))
            .GroupBy(item => item.Key)
            .Select(group => group.First()));

        FooterNavigationItems =
        [
            new NavigationItemViewModel(
                "Settings",
                LocalizedText.Key("Shadow.CurrentPage.Settings"),
                LocalizedText.Key("Shadow.Settings.Page.Subtitle"),
                FASymbol.Setting,
                SettingsPage),
        ];

        _pages = NavigationItems.ToDictionary(item => item.Key, item => item.Content);
        _pages["Settings"] = SettingsPage;

        CurrentPage = HomePage;
        ShadowLocalizer.Instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ShadowLocalizer.CultureName)
                or nameof(ShadowLocalizer.Version)
                or "Item[]"
                or null
                or "")
            {
                OnPropertyChanged(nameof(CurrentPageTitle));
                // Force the visible page to rebind so plugin AXAML Localizer[key] paths refresh.
                RefreshCurrentPage();
            }
        };
    }

    private readonly Dictionary<string, object> _pages;

    public HomeViewModel HomePage { get; } = new();

    public SettingsViewModel SettingsPage { get; }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public ObservableCollection<NavigationItemViewModel> FooterNavigationItems { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSelected))]
    [NotifyPropertyChangedFor(nameof(IsSettingsSelected))]
    [NotifyPropertyChangedFor(nameof(CurrentPageTitle))]
    private string _selectedPageKey = "Home";

    [ObservableProperty]
    private object _currentPage = null!;

    public bool IsHomeSelected => SelectedPageKey == "Home";

    public bool IsSettingsSelected => SelectedPageKey == "Settings";

    public string CurrentPageTitle => SelectedPageKey switch
    {
        "Settings" => Localizer["Shadow.CurrentPage.Settings"],
        _ => Localizer["Shadow.CurrentPage.Workstation"],
    };

    [RelayCommand]
    private void Navigate(string pageKey)
    {
        SelectedPageKey = pageKey;
        CurrentPage = _pages.TryGetValue(pageKey, out var page) ? page : HomePage;
    }

    private void RefreshCurrentPage()
    {
        var page = CurrentPage;
        CurrentPage = new object();
        CurrentPage = page;
    }
}
