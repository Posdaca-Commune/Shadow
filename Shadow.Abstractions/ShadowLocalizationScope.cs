namespace Shadow.Abstractions;

/// <summary>
/// Binding-friendly string lookup. ViewModels should replace this instance when the culture changes,
/// because Avalonia compiled bindings often do not re-read indexer values on the same object.
/// </summary>
public sealed class ShadowLocalizationScope
{
    public string this[string key] => ShadowLocalizer.Instance[key];

    public int Version => ShadowLocalizer.Instance.Version;

    public string CultureName
    {
        get => ShadowLocalizer.Instance.CultureName;
        set => ShadowLocalizer.Instance.CultureName = value;
    }

    public string Format(string key, params object[] args) => ShadowLocalizer.Instance.Format(key, args);
}
