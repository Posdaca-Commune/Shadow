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

    public override string ToString()
    {
        return Title;
    }
}
