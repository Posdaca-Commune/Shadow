using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Shadow.Models;

public partial class PersonalizationOptions : ObservableObject
{
    [ObservableProperty]
    private AppThemeMode _themeMode = AppThemeMode.System;

    [ObservableProperty]
    private WindowBackdropKind _backdrop = WindowBackdropKind.Mica;

    [ObservableProperty]
    private Color _accentColor = Color.Parse("#5B7CFA");

    [ObservableProperty]
    private bool _useSystemAccentColor = true;

    [ObservableProperty]
    private bool _showCompactSidebar;

    [ObservableProperty]
    private bool _enableAnimations = true;
}
