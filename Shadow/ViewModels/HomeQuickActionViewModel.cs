using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;

namespace Shadow.ViewModels;

public sealed class HomeQuickActionViewModel
{
    public HomeQuickActionViewModel(
        string key,
        string title,
        string description,
        FASymbol symbol,
        IRelayCommand command)
    {
        Key = key;
        Title = title;
        Description = description;
        Symbol = symbol;
        Command = command;
    }

    public string Key { get; }

    public string Title { get; }

    public string Description { get; }

    public FASymbol Symbol { get; }

    public IRelayCommand Command { get; }
}
