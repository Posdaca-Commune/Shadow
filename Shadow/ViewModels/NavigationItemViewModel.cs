using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;

namespace Shadow.ViewModels;

public sealed class NavigationItemViewModel : ViewModelBase
{
    private readonly string _title;
    private readonly string _description;

    public NavigationItemViewModel(string key, string title, string description, FASymbol symbol, object content)
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

    public NavigationItemViewModel(ShadowNavigationItem item)
        : this(
            item.Key,
            item.Title,
            item.Description,
            IconKeyResolver.Resolve(item.IconKey, FASymbol.Document),
            item.Content)
    {
    }

    public string Key { get; }

    public string Title => LocalizedText.Resolve(_title);

    public string Description => LocalizedText.Resolve(_description);

    public FASymbol Symbol { get; }

    public object Content { get; }
}
