using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shadow.Abstractions;
using Shadow.ParadoxGameLauncher.Localization;
using Shadow.ParadoxGameLauncher.Models;
using Shadow.ParadoxGameLauncher.Services;

namespace Shadow.ParadoxGameLauncher.ViewModels;

public sealed partial class ParadoxGameSettingsViewModel : ObservableObject
{
    private const int FullscreenModeCount = 3;
    private readonly ParadoxGameLauncherConfiguration _configuration;
    private readonly ParadoxGameLauncherService _service;
    private string _statusTextKey = "Paradox.GameSettings.Status.Default";
    private object[] _statusTextArgs = [];

    public ParadoxGameSettingsViewModel(ParadoxGameLauncherConfiguration configuration, ParadoxGameLauncherService service)
    {
        ParadoxGameLauncherStrings.Register();
        _configuration = configuration;
        _service = service;
        LoadFromSettings(_service.LoadGameSettings());
        ShadowLocalizer.Instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not (nameof(ShadowLocalizer.CultureName)
                or nameof(ShadowLocalizer.Version)
                or "Item[]"
                or null
                or ""))
            {
                return;
            }

            Localizer = new ShadowLocalizationScope();
            OnPropertyChanged(nameof(FullscreenModes));
            StatusText = ParadoxGameLauncherStrings.Format(_statusTextKey, _statusTextArgs);
        };
    }

    [ObservableProperty]
    private ShadowLocalizationScope _localizer = new();

    public string[] Languages { get; } =
    [
        "l_english",
        "l_simp_chinese",
        "l_french",
        "l_german",
        "l_spanish",
        "l_polish",
        "l_portuguese",
        "l_russian",
        "l_japanese",
    ];

    public IReadOnlyList<string> FullscreenModes =>
    [
        ParadoxGameLauncherStrings.Get("Paradox.Fullscreen.Window"),
        ParadoxGameLauncherStrings.Get("Paradox.Fullscreen.Fullscreen"),
        ParadoxGameLauncherStrings.Get("Paradox.Fullscreen.Borderless"),
    ];

    [ObservableProperty]
    private string _gameUserDirectory = string.Empty;

    [ObservableProperty]
    private string _gameExecutablePath = string.Empty;

    [ObservableProperty]
    private string _workshopDirectory = string.Empty;

    [ObservableProperty]
    private string _launchArguments = string.Empty;

    [ObservableProperty]
    private bool _closeAfterLaunch;

    [ObservableProperty]
    private string _selectedLanguage = "l_english";

    [ObservableProperty]
    private int _displayIndex;

    [ObservableProperty]
    private int _selectedFullscreenModeIndex = 1;

    [ObservableProperty]
    private int _resolutionWidth = 1920;

    [ObservableProperty]
    private int _resolutionHeight = 1080;

    [ObservableProperty]
    private int _refreshRate = 60;

    [ObservableProperty]
    private bool _vSync = true;

    [ObservableProperty]
    private double _masterVolume = 0.75;

    [ObservableProperty]
    private double _musicVolume = 0.5;

    [ObservableProperty]
    private double _effectsVolume = 0.75;

    [ObservableProperty]
    private double _interfaceVolume = 0.75;

    [ObservableProperty]
    private string _statusText = ParadoxGameLauncherStrings.Get("Paradox.GameSettings.Status.Default");

    [RelayCommand]
    private void Reload()
    {
        LoadFromSettings(_service.LoadGameSettings());
        SetStatusText("Paradox.GameSettings.Status.Reloaded");
    }

    [RelayCommand]
    private void Save()
    {
        SaveLauncherOptions();
        _service.SaveGameSettings(CreateSettings());
        SetStatusText("Paradox.GameSettings.Status.Saved");
    }

    public void SaveLauncherOptions()
    {
        _configuration.GameExecutablePath = GameExecutablePath;
        _configuration.GameUserDirectory = GameUserDirectory;
        _configuration.WorkshopDirectory = WorkshopDirectory;
        _configuration.LaunchArguments = LaunchArguments;
        _configuration.CloseAfterLaunch = CloseAfterLaunch;
        _configuration.Save();
    }

    partial void OnGameExecutablePathChanged(string value)
    {
        _configuration.GameExecutablePath = value;
        _configuration.Save();
    }

    partial void OnGameUserDirectoryChanged(string value)
    {
        _configuration.GameUserDirectory = value;
        _configuration.Save();
    }

    partial void OnWorkshopDirectoryChanged(string value)
    {
        _configuration.WorkshopDirectory = value;
        _configuration.Save();
    }

    partial void OnLaunchArgumentsChanged(string value)
    {
        _configuration.LaunchArguments = value;
        _configuration.Save();
    }

    partial void OnCloseAfterLaunchChanged(bool value)
    {
        _configuration.CloseAfterLaunch = value;
        _configuration.Save();
    }

    private void LoadFromSettings(GameSettings settings)
    {
        GameExecutablePath = _configuration.GameExecutablePath;
        GameUserDirectory = _configuration.GameUserDirectory;
        WorkshopDirectory = _configuration.WorkshopDirectory;
        LaunchArguments = _configuration.LaunchArguments;
        CloseAfterLaunch = _configuration.CloseAfterLaunch;
        SelectedLanguage = settings.Language;
        DisplayIndex = settings.DisplayIndex;
        SelectedFullscreenModeIndex = Math.Clamp(settings.FullscreenMode, 0, FullscreenModeCount - 1);
        ResolutionWidth = settings.ResolutionWidth;
        ResolutionHeight = settings.ResolutionHeight;
        RefreshRate = settings.RefreshRate;
        VSync = settings.VSync;
        MasterVolume = settings.MasterVolume;
        MusicVolume = settings.MusicVolume;
        EffectsVolume = settings.EffectsVolume;
        InterfaceVolume = settings.InterfaceVolume;
    }

    private GameSettings CreateSettings()
    {
        return new GameSettings
        {
            Language = SelectedLanguage,
            DisplayIndex = Math.Max(0, DisplayIndex),
            FullscreenMode = Math.Clamp(SelectedFullscreenModeIndex, 0, FullscreenModeCount - 1),
            ResolutionWidth = Math.Max(640, ResolutionWidth),
            ResolutionHeight = Math.Max(480, ResolutionHeight),
            RefreshRate = Math.Max(24, RefreshRate),
            VSync = VSync,
            MasterVolume = (float)Math.Clamp(MasterVolume, 0, 1),
            MusicVolume = (float)Math.Clamp(MusicVolume, 0, 1),
            EffectsVolume = (float)Math.Clamp(EffectsVolume, 0, 1),
            InterfaceVolume = (float)Math.Clamp(InterfaceVolume, 0, 1),
        };
    }

    private void SetStatusText(string key, params object[] args)
    {
        _statusTextKey = key;
        _statusTextArgs = args;
        StatusText = ParadoxGameLauncherStrings.Format(key, args);
    }
}




