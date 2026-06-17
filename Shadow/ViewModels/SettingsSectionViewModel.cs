using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;

namespace Shadow.ViewModels;

public partial class SettingsSectionViewModel : ViewModelBase
{
    public SettingsSectionViewModel(
        string key,
        string title,
        string description,
        FASymbol symbol,
        object? content = null)
    {
        Key = key;
        Title = title;
        Description = description;
        Symbol = symbol;
        Content = content;
    }

    public SettingsSectionViewModel(ShadowSettingsSection section)
        : this(
            section.Key,
            section.Title,
            section.Description,
            IconKeyResolver.Resolve(section.IconKey, FASymbol.Setting),
            section.Content)
    {
    }

    public string Key { get; }

    public string Title { get; }

    public string Description { get; }

    public FASymbol Symbol { get; }

    public object? Content { get; }

    [ObservableProperty]
    private bool _isSelected;
}
