using System;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;

namespace Shadow.ViewModels;

public sealed class HomeFeatureCardViewModel : ViewModelBase
{
    private readonly Func<ShadowHomeStatus?> _statusFactory;
    private readonly Action<string> _navigate;
    private ShadowHomeStatus _status;

    public HomeFeatureCardViewModel(
        string navigationKey,
        FASymbol symbol,
        Func<ShadowHomeStatus?> statusFactory,
        Action<string> navigate)
    {
        NavigationKey = navigationKey;
        Symbol = symbol;
        _statusFactory = statusFactory;
        _navigate = navigate;
        _status = statusFactory() ?? CreateUnavailableStatus();

        OpenCommand = new RelayCommand(() => _navigate(NavigationKey));
        LaunchCommand = new RelayCommand(Launch, () => CanLaunch);
        Refresh();
    }

    public string NavigationKey { get; }

    public FASymbol Symbol { get; }

    public IRelayCommand OpenCommand { get; }

    public IRelayCommand LaunchCommand { get; }

    public string Title => _status.Title;

    public string Summary => _status.Summary;

    public string? Detail => _status.Detail;

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool NeedsSetup => _status.NeedsSetup;

    public bool CanLaunch => _status is { CanLaunch: true, Launch: not null };

    public string PrimaryActionLabel => NeedsSetup
        ? Localizer["Shadow.Home.Action.Configure"]
        : Localizer["Shadow.Home.Action.Open"];

    public string LaunchActionLabel => Localizer["Shadow.Home.Action.Launch"];

    public string SetupBadgeLabel => Localizer["Shadow.Home.Badge.SetupNeeded"];

    public void Refresh()
    {
        _status = _statusFactory() ?? CreateUnavailableStatus();

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Detail));
        OnPropertyChanged(nameof(HasDetail));
        OnPropertyChanged(nameof(NeedsSetup));
        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(LaunchActionLabel));
        OnPropertyChanged(nameof(SetupBadgeLabel));
        LaunchCommand.NotifyCanExecuteChanged();
    }

    protected override void OnLocalizerChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        Refresh();
        base.OnLocalizerChanged(e);
    }

    private void Launch()
    {
        _status.Launch?.Invoke();
        Refresh();
    }

    private ShadowHomeStatus CreateUnavailableStatus() =>
        new(Localizer["Shadow.Home.Feature.Unavailable.Title"],
            Localizer["Shadow.Home.Feature.Unavailable.Summary"]);
}
