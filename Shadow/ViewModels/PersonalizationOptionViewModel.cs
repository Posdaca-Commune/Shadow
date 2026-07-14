using Shadow.Abstractions;

namespace Shadow.ViewModels;

public sealed class PersonalizationOptionViewModel<T>
{
    public PersonalizationOptionViewModel(T value, string title, string description)
    {
        Value = value;
        Title = title;
        Description = description;
    }

    public T Value { get; }

    public string Title { get; }

    public string Description { get; }

    public string DisplayTitle => LocalizedText.Resolve(Title);

    public string DisplayDescription => LocalizedText.Resolve(Description);

    public override string ToString()
    {
        return DisplayTitle;
    }
}
