namespace Shadow.Abstractions;

public static class LocalizedText
{
    public const string Prefix = "i18n:";

    public static string Key(string key)
    {
        return $"{Prefix}{key}";
    }

    public static bool IsKey(string text)
    {
        return text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string Resolve(string text)
    {
        return IsKey(text)
            ? ShadowLocalizer.Instance[text[Prefix.Length..]]
            : text;
    }
}
