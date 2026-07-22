using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Controls;
using Shadow.Models;
using Shadow.Abstractions;
using Shadow.Plugins;
using Shadow.Services;
using Shadow.Views;

namespace Shadow.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ApplicationSettings _applicationSettings;
    private bool _isLoadingPersonalization;

    public SettingsViewModel()
        : this([], [], ApplicationSettingsStore.Load())
    {
    }

    public SettingsViewModel(IReadOnlyList<ShadowSettingsSection> pluginSections)
        : this(pluginSections, [], ApplicationSettingsStore.Load())
    {
    }

    internal SettingsViewModel(
        IReadOnlyList<ShadowSettingsSection> pluginSections,
        IReadOnlyList<LoadedPlugin> loadedPlugins,
        ApplicationSettings applicationSettings)
    {
        _applicationSettings = applicationSettings;
        Sections =
        [
            new SettingsSectionViewModel(
                "Personalization",
                LocalizedText.Key("Shadow.Settings.Section.Personalization.Title"),
                LocalizedText.Key("Shadow.Settings.Section.Personalization.Description"),
                FASymbol.Setting),
            new SettingsSectionViewModel(
                "Plugins",
                LocalizedText.Key("Shadow.Settings.Section.Plugins.Title"),
                LocalizedText.Key("Shadow.Settings.Section.Plugins.Description"),
                FASymbol.AllApps),
            new SettingsSectionViewModel(
                "Workspace",
                LocalizedText.Key("Shadow.Settings.Section.Workspace.Title"),
                LocalizedText.Key("Shadow.Settings.Section.Workspace.Description"),
                FASymbol.Document),
        ];

        foreach (var section in pluginSections)
        {
            Sections.Add(new SettingsSectionViewModel(section));
        }

        Sections.Add(new SettingsSectionViewModel(
            "About",
            LocalizedText.Key("Shadow.Settings.Section.About.Title"),
            LocalizedText.Key("Shadow.Settings.Section.About.Description"),
            FASymbol.Help));

        LoadedPlugins = new ObservableCollection<PluginInfoViewModel>(
            loadedPlugins
                .Select(plugin => new PluginInfoViewModel(plugin))
                .OrderBy(plugin => plugin.DisplayName));

        _isLoadingPersonalization = true;
        ApplicationSettingsStore.ApplyToPersonalization(_applicationSettings, Personalization);
        SelectedLanguageIndex = ResolveLanguageIndex(_applicationSettings.Language);
        SelectedThemeModeIndex = Array.FindIndex(ThemeModes, option => option.Value == Personalization.ThemeMode);
        if (SelectedThemeModeIndex < 0)
        {
            SelectedThemeModeIndex = 0;
        }

        SelectedBackdropIndex = Array.FindIndex(BackdropKinds, option => option.Value == Personalization.Backdrop);
        if (SelectedBackdropIndex < 0)
        {
            SelectedBackdropIndex = 0;
        }

        SelectedSection = Sections[0];
        SelectedSection.IsSelected = true;
        Personalization.PropertyChanged += Personalization_OnPropertyChanged;
        ShadowLocalizer.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(LoadedPluginCountLabel));
            OnPropertyChanged(nameof(ThemeModeName));
            OnPropertyChanged(nameof(BackdropName));
        };
        _isLoadingPersonalization = false;
        ApplyPersonalization();
    }

    public ObservableCollection<SettingsSectionViewModel> Sections { get; }

    public ObservableCollection<PluginInfoViewModel> LoadedPlugins { get; }

    public PersonalizationOptions Personalization { get; } = new();

    public PersonalizationOptionViewModel<string>[] LanguageOptions { get; } =
    [
        new(ShadowLocalizer.DefaultCultureName, "简体中文", "Chinese (Simplified)"),
        new(ShadowLocalizer.EnglishCultureName, "English", "English"),
    ];

    public PersonalizationOptionViewModel<AppThemeMode>[] ThemeModes { get; } =
    [
        new(AppThemeMode.System, LocalizedText.Key("Shadow.Settings.Theme.System"),
            LocalizedText.Key("Shadow.Settings.Theme.System.Description")),
        new(AppThemeMode.Light, LocalizedText.Key("Shadow.Settings.Theme.Light"),
            LocalizedText.Key("Shadow.Settings.Theme.Light.Description")),
        new(AppThemeMode.Dark, LocalizedText.Key("Shadow.Settings.Theme.Dark"),
            LocalizedText.Key("Shadow.Settings.Theme.Dark.Description")),
    ];

    public PersonalizationOptionViewModel<WindowBackdropKind>[] BackdropKinds { get; } =
    [
        new(WindowBackdropKind.Mica, "Mica", LocalizedText.Key("Shadow.Settings.Backdrop.Mica.Description")),
        new(WindowBackdropKind.Acrylic, LocalizedText.Key("Shadow.Settings.Backdrop.Acrylic"),
            LocalizedText.Key("Shadow.Settings.Backdrop.Acrylic.Description")),
        new(WindowBackdropKind.Solid, LocalizedText.Key("Shadow.Settings.Backdrop.Solid"),
            LocalizedText.Key("Shadow.Settings.Backdrop.Solid.Description")),
    ];

    [ObservableProperty]
    private SettingsSectionViewModel _selectedSection = null!;

    [ObservableProperty]
    private int _selectedThemeModeIndex;

    [ObservableProperty]
    private int _selectedBackdropIndex;

    [ObservableProperty]
    private int _selectedLanguageIndex;

    public bool IsPersonalizationSelected => SelectedSection.Key == "Personalization";

    public bool IsPluginInventorySelected => SelectedSection.Key == "Plugins";

    public bool IsPluginSectionSelected => SelectedSection.Content is not null;

    public bool IsPlaceholderSectionSelected =>
        !IsPersonalizationSelected && !IsPluginInventorySelected && !IsPluginSectionSelected;

    public int LoadedPluginCount => LoadedPlugins.Count;

    public bool HasLoadedPlugins => LoadedPluginCount > 0;

    public bool IsNoPluginLoaded => !HasLoadedPlugins;

    public string LoadedPluginCountLabel => LoadedPluginCount == 0
        ? Localizer["Shadow.Settings.Plugins.Loaded.Zero"]
        : Localizer.Format("Shadow.Settings.Plugins.Loaded.Many", LoadedPluginCount);

    public string ThemeModeName => Personalization.ThemeMode switch
    {
        AppThemeMode.Light => Localizer["Shadow.Settings.Theme.Light"],
        AppThemeMode.Dark => Localizer["Shadow.Settings.Theme.Dark"],
        _ => Localizer["Shadow.Settings.Theme.System"],
    };

    public string BackdropName => Personalization.Backdrop switch
    {
        WindowBackdropKind.Acrylic => Localizer["Shadow.Settings.Backdrop.Acrylic"],
        WindowBackdropKind.Solid => Localizer["Shadow.Settings.Backdrop.Solid"],
        _ => "Mica",
    };

    partial void OnSelectedThemeModeIndexChanged(int value)
    {
        if (_isLoadingPersonalization || value < 0 || value >= ThemeModes.Length)
        {
            return;
        }

        Personalization.ThemeMode = ThemeModes[value].Value;
        OnPropertyChanged(nameof(ThemeModeName));
        ApplyThemeMode();
        PersistPersonalization();
    }

    partial void OnSelectedBackdropIndexChanged(int value)
    {
        if (_isLoadingPersonalization || value < 0 || value >= BackdropKinds.Length)
        {
            return;
        }

        Personalization.Backdrop = BackdropKinds[value].Value;
        OnPropertyChanged(nameof(BackdropName));
        ApplyBackdrop();
        PersistPersonalization();
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (_isLoadingPersonalization || value < 0 || value >= LanguageOptions.Length)
        {
            return;
        }

        ApplyLanguage(LanguageOptions[value].Value);
    }

    private void ApplyLanguage(string language)
    {
        var normalized = ShadowLocalizer.NormalizeCultureName(language);
        ShadowLocalizer.Instance.CultureName = normalized;
        _applicationSettings.Language = normalized;
        ApplicationSettingsStore.Save(_applicationSettings);
        OnPropertyChanged(nameof(LoadedPluginCountLabel));
        OnPropertyChanged(nameof(ThemeModeName));
        OnPropertyChanged(nameof(BackdropName));
    }

    partial void OnSelectedSectionChanged(SettingsSectionViewModel value)
    {
        foreach (var section in Sections)
        {
            section.IsSelected = section == value;
        }

        OnPropertyChanged(nameof(IsPersonalizationSelected));
        OnPropertyChanged(nameof(IsPluginInventorySelected));
        OnPropertyChanged(nameof(IsPluginSectionSelected));
        OnPropertyChanged(nameof(IsPlaceholderSectionSelected));
    }

    [RelayCommand]
    private void SelectSection(SettingsSectionViewModel section)
    {
        SelectedSection = section;
    }

    [RelayCommand]
    private void SetAccentColor(string color)
    {
        Personalization.AccentColor = Color.Parse(color);
        Personalization.UseSystemAccentColor = false;
    }

    [RelayCommand]
    private void UseSystemAccentColor()
    {
        Personalization.UseSystemAccentColor = true;
    }

    private void Personalization_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingPersonalization)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(PersonalizationOptions.UseSystemAccentColor):
            case nameof(PersonalizationOptions.AccentColor):
                ApplyAccentColor();
                PersistPersonalization();
                break;
            case nameof(PersonalizationOptions.ShowCompactSidebar):
                ApplyCompactSidebar();
                PersistPersonalization();
                break;
            case nameof(PersonalizationOptions.EnableAnimations):
                ApplyAnimationsPreference();
                PersistPersonalization();
                break;
            case nameof(PersonalizationOptions.ThemeMode):
                ApplyThemeMode();
                PersistPersonalization();
                break;
            case nameof(PersonalizationOptions.Backdrop):
                ApplyBackdrop();
                PersistPersonalization();
                break;
        }
    }

    private void ApplyPersonalization()
    {
        ApplyThemeMode();
        ApplyAccentColor();
        ApplyBackdrop();
        ApplyCompactSidebar();
        ApplyAnimationsPreference();
    }

    private void ApplyThemeMode()
    {
        var themeVariant = Personalization.ThemeMode switch
        {
            AppThemeMode.Light => ThemeVariant.Light,
            AppThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        if (Application.Current is not { } application)
        {
            return;
        }

        application.RequestedThemeVariant = themeVariant;

        var fluentTheme = TryGetFluentTheme(application);
        if (fluentTheme is not null)
        {
            fluentTheme.PreferSystemTheme = Personalization.ThemeMode == AppThemeMode.System;
        }

        if (fluentTheme is not null
            && application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                fluentTheme.ForceWin32WindowToTheme(window, themeVariant);
            }
        }
    }

    private void ApplyAccentColor()
    {
        if (Application.Current is not { } application
            || TryGetFluentTheme(application) is not { } fluentTheme)
        {
            return;
        }

        fluentTheme.PreferUserAccentColor = Personalization.UseSystemAccentColor;
        fluentTheme.CustomAccentColor = Personalization.UseSystemAccentColor
            ? null
            : Personalization.AccentColor;
    }

    private void ApplyBackdrop()
    {
        foreach (var window in EnumerateWindows())
        {
            if (window is MainWindow mainWindow)
            {
                mainWindow.ApplyBackdrop(Personalization.Backdrop);
            }
            else
            {
                ApplyBackdropToWindow(window, Personalization.Backdrop);
            }
        }
    }

    private void ApplyCompactSidebar()
    {
        foreach (var window in EnumerateWindows().OfType<MainWindow>())
        {
            window.ApplyCompactSidebar(Personalization.ShowCompactSidebar);
        }
    }

    private void ApplyAnimationsPreference()
    {
        ShadowUiPreferences.EnableAnimations = Personalization.EnableAnimations;
        foreach (var window in EnumerateWindows().OfType<MainWindow>())
        {
            window.ApplyAnimationsEnabled(Personalization.EnableAnimations);
        }
    }

    private void PersistPersonalization()
    {
        if (_isLoadingPersonalization)
        {
            return;
        }

        ApplicationSettingsStore.CaptureFromPersonalization(_applicationSettings, Personalization);
        ApplicationSettingsStore.Save(_applicationSettings);
    }

    internal static void ApplyBackdropToWindow(Window window, WindowBackdropKind backdrop)
    {
        switch (backdrop)
        {
            case WindowBackdropKind.Acrylic:
                window.TransparencyLevelHint =
                [
                    WindowTransparencyLevel.AcrylicBlur,
                    WindowTransparencyLevel.Blur,
                    WindowTransparencyLevel.Transparent,
                ];
                window.Background = Brushes.Transparent;
                break;
            case WindowBackdropKind.Solid:
                window.TransparencyLevelHint = [WindowTransparencyLevel.None];
                var isLight = window.ActualThemeVariant == ThemeVariant.Light;
                window.Background = new SolidColorBrush(Color.Parse(isLight ? "#F3F3F3" : "#202020"));
                break;
            default:
                window.TransparencyLevelHint =
                [
                    WindowTransparencyLevel.Mica,
                    WindowTransparencyLevel.AcrylicBlur,
                    WindowTransparencyLevel.Blur,
                ];
                window.Background = Brushes.Transparent;
                break;
        }
    }

    private static IEnumerable<Window> EnumerateWindows()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.Windows;
        }

        return [];
    }

    private static FluentAvaloniaTheme? TryGetFluentTheme(Application application)
    {
        return application.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
    }

    private int ResolveLanguageIndex(string language)
    {
        var normalizedLanguage = ShadowLocalizer.NormalizeCultureName(language);
        var index = Array.FindIndex(
            LanguageOptions,
            option => string.Equals(option.Value, normalizedLanguage, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? 0 : index;
    }
}



