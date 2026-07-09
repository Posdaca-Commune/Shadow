using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
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

namespace Shadow.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel()
        : this([], [])
    {
    }

    public SettingsViewModel(IReadOnlyList<ShadowSettingsSection> pluginSections)
        : this(pluginSections, [])
    {
    }

    internal SettingsViewModel(
        IReadOnlyList<ShadowSettingsSection> pluginSections,
        IReadOnlyList<LoadedPlugin> loadedPlugins)
    {
        Sections =
        [
            new SettingsSectionViewModel("Personalization", "个性化", "主题、颜色和窗口材质", FASymbol.Setting),
            new SettingsSectionViewModel("Plugins", "插件", "插件加载与扩展能力", FASymbol.AllApps),
            new SettingsSectionViewModel("Workspace", "工作区", "HOI4 项目路径和缓存", FASymbol.Document),
        ];

        foreach (var section in pluginSections)
        {
            Sections.Add(new SettingsSectionViewModel(section));
        }

        Sections.Add(new SettingsSectionViewModel("About", "关于", "版本和运行环境", FASymbol.Help));

        LoadedPlugins = new ObservableCollection<PluginInfoViewModel>(
            loadedPlugins
                .Select(plugin => new PluginInfoViewModel(plugin))
                .OrderBy(plugin => plugin.DisplayName));
        SelectedSection = Sections[0];
        SelectedSection.IsSelected = true;
        Personalization.PropertyChanged += Personalization_OnPropertyChanged;
        ApplyPersonalization();
    }

    public ObservableCollection<SettingsSectionViewModel> Sections { get; }

    public ObservableCollection<PluginInfoViewModel> LoadedPlugins { get; }

    public PersonalizationOptions Personalization { get; } = new();

    public PersonalizationOptionViewModel<AppThemeMode>[] ThemeModes { get; } =
    [
        new(AppThemeMode.System, "跟随系统", "使用 Windows 当前颜色模式"),
        new(AppThemeMode.Light, "浅色", "提高日间和高亮环境可读性"),
        new(AppThemeMode.Dark, "深色", "降低暗光环境下的视觉负担"),
    ];

    public PersonalizationOptionViewModel<WindowBackdropKind>[] BackdropKinds { get; } =
    [
        new(WindowBackdropKind.Mica, "Mica", "与 Windows 11 桌面材质保持一致"),
        new(WindowBackdropKind.Acrylic, "亚克力", "保留更明显的透明层次"),
        new(WindowBackdropKind.Solid, "纯色", "使用稳定背景以减少视觉干扰"),
    ];

    [ObservableProperty]
    private SettingsSectionViewModel _selectedSection = null!;

    [ObservableProperty]
    private int _selectedThemeModeIndex;

    [ObservableProperty]
    private int _selectedBackdropIndex;

    public bool IsPersonalizationSelected => SelectedSection.Key == "Personalization";

    public bool IsPluginInventorySelected => SelectedSection.Key == "Plugins";

    public bool IsPluginSectionSelected => SelectedSection.Content is not null;

    public bool IsPlaceholderSectionSelected =>
        !IsPersonalizationSelected && !IsPluginInventorySelected && !IsPluginSectionSelected;

    public int LoadedPluginCount => LoadedPlugins.Count;

    public bool HasLoadedPlugins => LoadedPluginCount > 0;

    public bool IsNoPluginLoaded => !HasLoadedPlugins;

    public string LoadedPluginCountLabel => LoadedPluginCount == 0
        ? "当前没有加载插件"
        : $"当前已加载 {LoadedPluginCount} 个插件";

    public string ThemeModeName => Personalization.ThemeMode switch
    {
        AppThemeMode.Light => "浅色",
        AppThemeMode.Dark => "深色",
        _ => "跟随系统",
    };

    public string BackdropName => Personalization.Backdrop switch
    {
        WindowBackdropKind.Acrylic => "亚克力",
        WindowBackdropKind.Solid => "纯色",
        _ => "Mica",
    };

    partial void OnSelectedThemeModeIndexChanged(int value)
    {
        Personalization.ThemeMode = ThemeModes[value].Value;
        OnPropertyChanged(nameof(ThemeModeName));
        ApplyThemeMode();
    }

    partial void OnSelectedBackdropIndexChanged(int value)
    {
        Personalization.Backdrop = BackdropKinds[value].Value;
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
        if (e.PropertyName is nameof(PersonalizationOptions.UseSystemAccentColor)
            or nameof(PersonalizationOptions.AccentColor))
        {
            ApplyAccentColor();
        }
    }

    private void ApplyPersonalization()
    {
        ApplyThemeMode();
        ApplyAccentColor();
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

    private static FluentAvaloniaTheme? TryGetFluentTheme(Application application)
    {
        return application.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
    }
}
