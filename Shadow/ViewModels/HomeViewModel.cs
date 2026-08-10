using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;

namespace Shadow.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly Action<string> _navigate;
    private readonly IReadOnlyList<NavigationItemViewModel> _featureNavigationItems;

    public HomeViewModel()
        : this([], _ => { })
    {
    }

    internal HomeViewModel(
        IEnumerable<NavigationItemViewModel> featureNavigationItems,
        Action<string> navigate)
    {
        _navigate = navigate;
        _featureNavigationItems = featureNavigationItems
            .Where(item => !string.Equals(item.Key, "Home", StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(item.Key, "Settings", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        FeatureCards = new ObservableCollection<HomeFeatureCardViewModel>(
            _featureNavigationItems.Select(CreateFeatureCard));

        OpenSettingsCommand = new RelayCommand(() => _navigate("Settings"));
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public string Title { get; } = "Shadow";

    public string Subtitle => Localizer["Shadow.Home.Subtitle"];

    public string HeroDescription => Localizer["Shadow.Home.Hero.Description"];

    public string QuickActionsTitle => Localizer["Shadow.Home.QuickActions.Title"];

    public string FeaturesTitle => Localizer["Shadow.Home.Features.Title"];

    public string GettingStartedTitle => Localizer["Shadow.Home.GettingStarted.Title"];

    public string EmptyPluginsTitle => Localizer["Shadow.Home.Empty.Title"];

    public string EmptyPluginsDescription => Localizer["Shadow.Home.Empty.Description"];

    public string RefreshActionLabel => Localizer["Shadow.Home.Action.Refresh"];

    public string OpenSettingsActionLabel => Localizer["Shadow.Home.Action.OpenSettings"];

    public string VersionLabel => Localizer.Format("Shadow.Home.Version.Label", AppVersion);

    public string AppVersion { get; } = ResolveAppVersion();

    public ObservableCollection<HomeFeatureCardViewModel> FeatureCards { get; }

    public ObservableCollection<HomeQuickActionViewModel> QuickActions { get; } = [];

    public ObservableCollection<string> GettingStartedTips { get; } = [];

    public int FeatureCount => FeatureCards.Count;

    public int ReadyFeatureCount => FeatureCards.Count(card => !card.NeedsSetup);

    public int SetupNeededCount => FeatureCards.Count(card => card.NeedsSetup);

    public string FeatureCountLabel => Localizer.Format("Shadow.Home.Stat.Features", FeatureCount);

    public string ReadyCountLabel => Localizer.Format("Shadow.Home.Stat.Ready", ReadyFeatureCount);

    public string SetupNeededLabel => Localizer.Format("Shadow.Home.Stat.SetupNeeded", SetupNeededCount);

    public bool HasFeatures => FeatureCards.Count > 0;

    public bool HasNoFeatures => !HasFeatures;

    public bool ShowGettingStarted => GettingStartedTips.Count > 0;

    public IRelayCommand OpenSettingsCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    public void Refresh()
    {
        foreach (var card in FeatureCards)
        {
            card.Refresh();
        }

        RebuildQuickActions();
        RebuildGettingStartedTips();
        NotifyDashboardProperties();
    }

    protected override void OnLocalizerChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        Refresh();
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(HeroDescription));
        OnPropertyChanged(nameof(QuickActionsTitle));
        OnPropertyChanged(nameof(FeaturesTitle));
        OnPropertyChanged(nameof(GettingStartedTitle));
        OnPropertyChanged(nameof(EmptyPluginsTitle));
        OnPropertyChanged(nameof(EmptyPluginsDescription));
        OnPropertyChanged(nameof(RefreshActionLabel));
        OnPropertyChanged(nameof(OpenSettingsActionLabel));
        OnPropertyChanged(nameof(VersionLabel));
        base.OnLocalizerChanged(e);
    }

    private HomeFeatureCardViewModel CreateFeatureCard(NavigationItemViewModel item)
    {
        return new HomeFeatureCardViewModel(
            item.Key,
            item.Symbol,
            () =>
            {
                if (item.Content is IShadowHomeStatusProvider provider)
                {
                    return provider.GetHomeStatus();
                }

                return new ShadowHomeStatus(
                    item.Title,
                    item.Description,
                    Localizer["Shadow.Home.Feature.Generic.Detail"]);
            },
            _navigate);
    }

    private void RebuildQuickActions()
    {
        QuickActions.Clear();

        foreach (var item in _featureNavigationItems)
        {
            var key = item.Key;
            QuickActions.Add(new HomeQuickActionViewModel(
                key,
                item.Title,
                item.Description,
                item.Symbol,
                new RelayCommand(() => _navigate(key))));
        }

        QuickActions.Add(new HomeQuickActionViewModel(
            "Settings",
            Localizer["Shadow.CurrentPage.Settings"],
            Localizer["Shadow.Settings.Page.Subtitle"],
            FASymbol.Setting,
            OpenSettingsCommand));
    }

    private void RebuildGettingStartedTips()
    {
        GettingStartedTips.Clear();

        if (HasNoFeatures)
        {
            GettingStartedTips.Add(Localizer["Shadow.Home.Tip.InstallPlugin"]);
            GettingStartedTips.Add(Localizer["Shadow.Home.Tip.OpenSettings"]);
            return;
        }

        if (SetupNeededCount > 0)
        {
            GettingStartedTips.Add(Localizer["Shadow.Home.Tip.ConfigureGame"]);
        }

        GettingStartedTips.Add(Localizer["Shadow.Home.Tip.ManagePlaysets"]);
        GettingStartedTips.Add(Localizer["Shadow.Home.Tip.UseCommandLine"]);
    }

    private void NotifyDashboardProperties()
    {
        OnPropertyChanged(nameof(FeatureCount));
        OnPropertyChanged(nameof(ReadyFeatureCount));
        OnPropertyChanged(nameof(SetupNeededCount));
        OnPropertyChanged(nameof(FeatureCountLabel));
        OnPropertyChanged(nameof(ReadyCountLabel));
        OnPropertyChanged(nameof(SetupNeededLabel));
        OnPropertyChanged(nameof(HasFeatures));
        OnPropertyChanged(nameof(HasNoFeatures));
        OnPropertyChanged(nameof(ShowGettingStarted));
    }

    private static string ResolveAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+');
            return plusIndex > 0 ? informational[..plusIndex] : informational;
        }

        var version = assembly.GetName().Version;
        return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}

