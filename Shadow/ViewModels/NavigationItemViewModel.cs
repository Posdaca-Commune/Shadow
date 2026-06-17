using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;

namespace Shadow.ViewModels;

public sealed class NavigationItemViewModel
{
    public NavigationItemViewModel(string key, string title, string description, FASymbol symbol, object content)
    {
        Key = key;
        Title = title;
        Description = description;
        Symbol = symbol;
        Content = content;
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

    public string Title { get; }

    public string Description { get; }

    public FASymbol Symbol { get; }

    public object Content { get; }
}
