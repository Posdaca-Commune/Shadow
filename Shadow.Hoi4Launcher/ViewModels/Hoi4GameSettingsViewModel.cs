using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shadow.Hoi4Launcher.Models;
using Shadow.Hoi4Launcher.Services;

namespace Shadow.Hoi4Launcher.ViewModels;

public sealed partial class Hoi4GameSettingsViewModel : ObservableObject
{
    private readonly Hoi4LauncherConfiguration _configuration;
    private readonly Hoi4LauncherService _service;

    public Hoi4GameSettingsViewModel(Hoi4LauncherConfiguration configuration, Hoi4LauncherService service)
    {
        _configuration = configuration;
        _service = service;
        LoadFromSettings(_service.LoadGameSettings());
    }

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

    public string[] FullscreenModes { get; } =
    [
        "窗口",
        "全屏",
        "无边框窗口",
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
    private string _statusText = "游戏选项会写入 HOI4 用户目录的 settings.txt。";

    [RelayCommand]
    private void Reload()
    {
        LoadFromSettings(_service.LoadGameSettings());
        StatusText = "已重新读取 settings.txt。";
    }

    [RelayCommand]
    private void Save()
    {
        SaveLauncherOptions();
        _service.SaveGameSettings(CreateSettings());
        StatusText = "已保存 HOI4 游戏选项。";
    }

    public void SaveLauncherOptions()
    {
        _configuration.State.GameExecutablePath = GameExecutablePath;
        _configuration.State.GameUserDirectory = GameUserDirectory;
        _configuration.State.WorkshopDirectory = WorkshopDirectory;
        _configuration.State.LaunchArguments = LaunchArguments;
        _configuration.State.CloseAfterLaunch = CloseAfterLaunch;
        _configuration.Save();
    }

    partial void OnGameExecutablePathChanged(string value)
    {
        _configuration.State.GameExecutablePath = value;
        _configuration.Save();
    }

    partial void OnGameUserDirectoryChanged(string value)
    {
        _configuration.State.GameUserDirectory = value;
        _configuration.Save();
    }

    partial void OnWorkshopDirectoryChanged(string value)
    {
        _configuration.State.WorkshopDirectory = value;
        _configuration.Save();
    }

    partial void OnLaunchArgumentsChanged(string value)
    {
        _configuration.State.LaunchArguments = value;
        _configuration.Save();
    }

    partial void OnCloseAfterLaunchChanged(bool value)
    {
        _configuration.State.CloseAfterLaunch = value;
        _configuration.Save();
    }

    private void LoadFromSettings(GameSettings settings)
    {
        GameExecutablePath = _configuration.State.GameExecutablePath;
        GameUserDirectory = _configuration.State.GameUserDirectory;
        WorkshopDirectory = _configuration.State.WorkshopDirectory;
        LaunchArguments = _configuration.State.LaunchArguments;
        CloseAfterLaunch = _configuration.State.CloseAfterLaunch;
        SelectedLanguage = settings.Language;
        DisplayIndex = settings.DisplayIndex;
        SelectedFullscreenModeIndex = Math.Clamp(settings.FullscreenMode, 0, FullscreenModes.Length - 1);
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
            FullscreenMode = Math.Clamp(SelectedFullscreenModeIndex, 0, FullscreenModes.Length - 1),
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
}
