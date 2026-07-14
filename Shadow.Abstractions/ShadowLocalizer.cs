using System.ComponentModel;
using System.Globalization;

namespace Shadow.Abstractions;

public sealed class ShadowLocalizer : INotifyPropertyChanged
{
    public const string DefaultCultureName = "zh-CN";
    public const string EnglishCultureName = "en-US";

    public static ShadowLocalizer Instance { get; } = new();

    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<string, string>> _resources = new(StringComparer.OrdinalIgnoreCase);
    private string _cultureName = DefaultCultureName;
    private int _version;

    private ShadowLocalizer()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CultureName
    {
        get
        {
            lock (_gate)
            {
                return _cultureName;
            }
        }
        set => SetCulture(value);
    }

    public int Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    public string this[string key] => GetString(key);

    public bool HasKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        lock (_gate)
        {
            return (_resources.TryGetValue(_cultureName, out var current) && current.ContainsKey(key))
                   || (_resources.TryGetValue(DefaultCultureName, out var fallback) && fallback.ContainsKey(key))
                   || (_resources.TryGetValue(EnglishCultureName, out var english) && english.ContainsKey(key));
        }
    }

    public bool HasCultureKey(string cultureName, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalizedCultureName = NormalizeCultureName(cultureName);
        lock (_gate)
        {
            return _resources.TryGetValue(normalizedCultureName, out var strings)
                   && strings.ContainsKey(key);
        }
    }

    public void RegisterCulture(string cultureName, IReadOnlyDictionary<string, string> strings)
    {
        if (strings.Count == 0)
        {
            return;
        }

        var normalizedCultureName = NormalizeCultureName(cultureName);
        lock (_gate)
        {
            if (!_resources.TryGetValue(normalizedCultureName, out var cultureResources))
            {
                cultureResources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _resources[normalizedCultureName] = cultureResources;
            }

            foreach (var (key, value) in strings)
            {
                cultureResources[key] = value;
            }

            _version++;
        }

        RaiseAllStringsChanged();
    }

    public void SetCulture(string cultureName)
    {
        var normalizedCultureName = NormalizeCultureName(cultureName);
        var changed = false;
        lock (_gate)
        {
            if (!string.Equals(_cultureName, normalizedCultureName, StringComparison.OrdinalIgnoreCase))
            {
                _cultureName = normalizedCultureName;
                _version++;
                changed = true;
            }
        }

        TrySetThreadCulture(normalizedCultureName);

        if (!changed)
        {
            return;
        }

        RaiseAllStringsChanged();
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        lock (_gate)
        {
            if (TryGetString(_cultureName, key, out var localized))
            {
                return localized;
            }

            if (TryGetString(DefaultCultureName, key, out localized)
                || TryGetString(EnglishCultureName, key, out localized))
            {
                return localized;
            }
        }

        return key;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentUICulture, GetString(key), args);
    }

    public static string NormalizeCultureName(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return DefaultCultureName;
        }

        return cultureName.Trim().ToLowerInvariant() switch
        {
            "zh" or "zh-cn" or "zh-hans" => DefaultCultureName,
            "en" or "en-us" => EnglishCultureName,
            var value => value,
        };
    }

    private bool TryGetString(string cultureName, string key, out string value)
    {
        value = string.Empty;
        return _resources.TryGetValue(cultureName, out var strings)
               && strings.TryGetValue(key, out value!);
    }

    private static void TrySetThreadCulture(string cultureName)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            // Invalid persisted culture values should not prevent the app from starting.
        }
    }

    private void RaiseAllStringsChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CultureName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}

