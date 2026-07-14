using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;

namespace Shadow.ViewModels;

public partial class SettingsSectionViewModel : ViewModelBase
{
    private readonly string _title;
    private readonly string _description;

    public SettingsSectionViewModel(
        string key,
        string title,
        string description,
        FASymbol symbol,
        object? content = null)
    {
        Key = key;
        _title = title;
        _description = description;
        Symbol = symbol;
        Content = content;
        ShadowLocalizer.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
        };
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

    public string Title => LocalizedText.Resolve(_title);

    public string Description => LocalizedText.Resolve(_description);

    public FASymbol Symbol { get; }

    public object? Content { get; }

    [ObservableProperty]
    private bool _isSelected;
}
