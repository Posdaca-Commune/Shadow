using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;
using Shadow.ParadoxGameLauncher.Localization;
using Shadow.ParadoxGameLauncher.Models;
using Shadow.ParadoxGameLauncher.Services;

namespace Shadow.ParadoxGameLauncher.ViewModels;

public sealed partial class ParadoxGameLauncherViewModel : ObservableObject
{
    private readonly ParadoxGameLauncherConfiguration _configuration;
    private readonly ParadoxGameLauncherService _service;
    private readonly IShadowHostContext _hostContext;
    private string _statusTextKey = "Paradox.Status.Default";
    private object[] _statusTextArgs = [];
    public ParadoxGameLauncherViewModel(
        ParadoxGameLauncherConfiguration configuration,
        ParadoxGameLauncherService service,
        IShadowHostContext hostContext)
    {
        ParadoxGameLauncherStrings.Register();
        _configuration = configuration;
        _service = service;
        _hostContext = hostContext;
        _selectedGame = configuration.SelectedGame;
        GameSettings = new ParadoxGameSettingsViewModel(configuration, service);
        GameSettings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(ParadoxGameSettingsViewModel.GameExecutablePath)
                or nameof(ParadoxGameSettingsViewModel.GameUserDirectory)
                or nameof(ParadoxGameSettingsViewModel.WorkshopDirectory)
                or nameof(ParadoxGameSettingsViewModel.LaunchArguments)
                or nameof(ParadoxGameSettingsViewModel.CloseAfterLaunch))
            {
                NotifyLauncherOptionProperties();
            }
        };
        ReloadStoredPlaysets();
        SelectedSection = Sections[0];
        ShadowLocalizer.Instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not (nameof(ShadowLocalizer.CultureName)
                or nameof(ShadowLocalizer.Version)
                or "Item[]"
                or null
                or ""))
            {
                return;
            }
            Localizer = new ShadowLocalizationScope();
            if (!string.IsNullOrEmpty(_statusTextKey))
            {
                StatusText = ParadoxGameLauncherStrings.Format(_statusTextKey, _statusTextArgs);
            }
            OnPropertyChanged(nameof(ActivePlaysetName));
            OnPropertyChanged(nameof(SelectedPlaysetEditStateText));
            OnPropertyChanged(nameof(SelectedPlaysetSummaryText));
        };
        Refresh();
    }

    [ObservableProperty]
    private ShadowLocalizationScope _localizer = new();
    public IReadOnlyList<LauncherSection> Sections { get; } =
    [
        new("Home", LocalizedText.Key("Paradox.Section.Home.Title"),
            LocalizedText.Key("Paradox.Section.Home.Description"), FASymbol.Home),
        new("Mods", LocalizedText.Key("Paradox.Section.Mods.Title"),
            LocalizedText.Key("Paradox.Section.Mods.Description"), FASymbol.Library),
        new("Dlcs", LocalizedText.Key("Paradox.Section.Dlcs.Title"),
            LocalizedText.Key("Paradox.Section.Dlcs.Description"), FASymbol.Shop),
        new("Playsets", LocalizedText.Key("Paradox.Section.Playsets.Title"),
            LocalizedText.Key("Paradox.Section.Playsets.Description"), FASymbol.BulletList),
        new("GameSettings", LocalizedText.Key("Paradox.Section.GameSettings.Title"),
            LocalizedText.Key("Paradox.Section.GameSettings.Description"), FASymbol.Setting),
    ];
    public ObservableCollection<ModEntry> Mods { get; } = [];
    public ObservableCollection<ModEntry> FilteredMods { get; } = [];
    public ObservableCollection<PlaysetModEntry> PlaysetMods { get; } = [];
    public ObservableCollection<PlaysetModEntry> FilteredPlaysetMods { get; } = [];
    public ObservableCollection<ModEntry> AvailableMods { get; } = [];
    public ObservableCollection<ModEntry> FilteredAvailableMods { get; } = [];
    public ObservableCollection<DlcEntry> Dlcs { get; } = [];
    public ObservableCollection<Playset> Playsets { get; } = [];
    public ParadoxGameSettingsViewModel GameSettings { get; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSelected))]
    [NotifyPropertyChangedFor(nameof(IsModsSelected))]
    [NotifyPropertyChangedFor(nameof(IsDlcsSelected))]
    [NotifyPropertyChangedFor(nameof(IsPlaysetsSelected))]
    [NotifyPropertyChangedFor(nameof(IsGameSettingsSelected))]
    private LauncherSection _selectedSection = null!;
    public IReadOnlyList<ParadoxGameDefinition> AvailableGames => ParadoxGameCatalog.Games;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedGameDisplayName))]
    [NotifyPropertyChangedFor(nameof(SelectedPlaysetStorageDirectory))]
    private ParadoxGameDefinition _selectedGame = null!;
    public string SelectedGameDisplayName => SelectedGame.DisplayName;
    [RelayCommand]
    private void SelectGame(ParadoxGameDefinition? game)
    {
        if (game is null)
        {
            return;
        }
        SelectedGame = game;
    }

    [ObservableProperty] private Playset? _selectedPlayset;
    [ObservableProperty] private ModEntry? _selectedMod;
    [ObservableProperty] private PlaysetModEntry? _selectedPlaysetMod;
    [ObservableProperty] private ModEntry? _selectedAvailableMod;
    [ObservableProperty] private string _newPlaysetName = string.Empty;
    [ObservableProperty] private string _modSearchText = string.Empty;
    [ObservableProperty] private string _playsetModSearchText = string.Empty;
    [ObservableProperty] private string _availableModSearchText = string.Empty;
    [ObservableProperty] private string _statusText = ParadoxGameLauncherStrings.Get("Paradox.Status.Default");
    public int EnabledModCount => PlaysetMods.Count(mod => mod.IsEnabled);
    public int PlaysetModCount => PlaysetMods.Count;
    public int DisabledDlcCount => Dlcs.Count(dlc => !dlc.IsEnabled);
    public int ExternalPlaysetCount => Playsets.Count(playset => playset.IsExternal);
    public int PlaysetCount => Playsets.Count;
    public string ActivePlaysetName => SelectedPlayset?.Name ?? ParadoxGameLauncherStrings.Get("Paradox.Playsets.NoSelection");
    public bool CanEditSelectedPlayset => SelectedPlayset?.CanEdit == true;
    public string SelectedPlaysetEditStateText => SelectedPlayset?.CanEdit == false
        ? ParadoxGameLauncherStrings.Get("Paradox.Playsets.ReadOnly")
        : ParadoxGameLauncherStrings.Get("Paradox.Playsets.Editable");
    public string SelectedPlaysetSummaryText => ParadoxGameLauncherStrings.Format(
        "Paradox.Playsets.Summary",
        PlaysetModCount,
        EnabledModCount,
        SelectedPlaysetEditStateText);
    public string SelectedPlaysetStorageDirectory =>
        Path.Combine(_configuration.PlaysetStore.WorkspaceDirectory, "playsets");
    public string GameExecutablePath => _configuration.GameExecutablePath;
    public string GameUserDirectory => _configuration.GameUserDirectory;
    public string WorkshopDirectory => _configuration.WorkshopDirectory;
    public string LaunchArguments => _configuration.LaunchArguments;
    public bool CloseAfterLaunch => _configuration.CloseAfterLaunch;
    public bool IsHomeSelected => SelectedSection.Key == "Home";
    public bool IsModsSelected => SelectedSection.Key == "Mods";
    public bool IsDlcsSelected => SelectedSection.Key == "Dlcs";
    public bool IsPlaysetsSelected => SelectedSection.Key == "Playsets";
    public bool IsGameSettingsSelected => SelectedSection.Key == "GameSettings";
    partial void OnSelectedSectionChanged(LauncherSection value)
    {
        if (value.Key == "GameSettings")
        {
            GameSettings.ReloadCommand.Execute(null);
        }
        NotifyLauncherOptionProperties();
    }

    partial void OnSelectedGameChanged(ParadoxGameDefinition value)
    {
        if (string.Equals(_configuration.SelectedGame.Id, value.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        _configuration.SelectGame(value.Id);
        GameSettings.ReloadCommand.Execute(null);
        NotifyLauncherOptionProperties();
        OnPropertyChanged(nameof(SelectedPlaysetStorageDirectory));
        Refresh();
        SetLocalizedStatusText("Paradox.Status.SwitchedGame", value.DisplayName);
    }

    partial void OnSelectedPlaysetChanged(Playset? value)
    {
        if (value is null)
        {
            return;
        }
        _configuration.SelectedPlaysetId = value.Id;
        _configuration.Save();
        ApplySelectedPlaysetState();
        OnPropertyChanged(nameof(ActivePlaysetName));
        OnSelectionChanged();
    }

    [RelayCommand]
    private void Refresh()
    {
        SaveHostPaths();
        ReloadStoredPlaysets();
        Mods.Clear();
        foreach (var mod in _service.DiscoverMods())
        {
            Mods.Add(mod);
        }
        try
        {
            _configuration.PlaysetStore.SaveModIndex(Mods);
        }
        catch (Exception ex)
        {
            SetLocalizedStatusText("Paradox.Status.ModIndexFailed", ex.Message);
        }
        Dlcs.Clear();
        foreach (var dlc in _service.DiscoverDlcs())
        {
            dlc.PropertyChanged += (_, _) => OnSelectionChanged();
            Dlcs.Add(dlc);
        }
        ApplySelectedPlaysetState();
        RebuildAvailableMods();
        RebuildFilteredMods();
        NotifyLauncherOptionProperties();
        SetLocalizedStatusText("Paradox.Status.Refreshed", Mods.Count, Dlcs.Count);
    }

    [RelayCommand]
    private void AddPlayset()
    {
        var playsetName = string.IsNullOrWhiteSpace(NewPlaysetName)
            ? ParadoxGameLauncherStrings.Get("Paradox.Playset.New")
            : NewPlaysetName.Trim();
        var playset = CreateLocalPlayset(playsetName);
        Playsets.Add(playset);
        _configuration.PlaysetStore.SavePlayset(playset);
        SelectedPlayset = playset;
        NewPlaysetName = string.Empty;
        _configuration.Save();
        OnPlaysetCollectionChanged();
        SetLocalizedStatusText("Paradox.Status.CreatedPlayset", playset.Name);
    }

    public void AddPlayset(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        NewPlaysetName = name.Trim();
        AddPlayset();
    }

    [RelayCommand]
    private void DeletePlayset()
    {
        if (SelectedPlayset is null)
        {
            return;
        }
        var playset = SelectedPlayset;
        if (!playset.CanEdit)
        {
            SetLocalizedStatusText("Paradox.Status.CannotDeletePlayset", playset.Name);
            return;
        }
        if (Playsets.Count <= 1)
        {
            SetLocalizedStatusText("Paradox.Status.NeedOnePlayset");
            return;
        }
        var index = Playsets.IndexOf(playset);
        Playsets.Remove(playset);
        _configuration.PlaysetStore.DeletePlayset(playset);
        SelectedPlayset = Playsets.ElementAtOrDefault(Math.Clamp(index, 0, Playsets.Count - 1))
                          ?? Playsets.FirstOrDefault();
        _configuration.Save();
        OnPlaysetCollectionChanged();
        SetLocalizedStatusText("Paradox.Status.DeletedPlayset", playset.Name);
    }

    [RelayCommand]
    private void SavePlayset()
    {
        if (SelectedPlayset is null)
        {
            return;
        }
        if (!CanEditSelectedPlayset)
        {
            _service.ApplyPlayset(SelectedPlayset, PlaysetMods.Select(mod => mod.Mod), Dlcs);
            SetLocalizedStatusText("Paradox.Status.AppliedReadOnlyPlayset", SelectedPlayset.Name);
            return;
        }
        PersistCurrentPlaysetState();
        _service.ApplyPlayset(SelectedPlayset, PlaysetMods.Select(mod => mod.Mod), Dlcs);
        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(PlaysetModCount));
        OnPropertyChanged(nameof(DisabledDlcCount));
        SetLocalizedStatusText("Paradox.Status.SavedAppliedPlayset", SelectedPlayset.Name);
    }

    [RelayCommand]
    private void ImportParadoxPlaysets()
    {
        SaveHostPaths();
        var importedPlaysets = _service.ImportParadoxPlaysets(Mods);
        if (importedPlaysets.Count == 0)
        {
            SetLocalizedStatusText("Paradox.Status.NoParadoxPlaysets");
            return;
        }
        foreach (var importedPlayset in importedPlaysets)
        {
            var existing = Playsets.FirstOrDefault(playset => playset.Id == importedPlayset.Id);
            if (existing is null)
            {
                Playsets.Add(importedPlayset);
                _configuration.PlaysetStore.SavePlayset(importedPlayset);
                continue;
            }
            existing.Name = importedPlayset.Name;
            existing.EnabledModIds = importedPlayset.EnabledModIds;
            existing.ModIds = importedPlayset.ModIds.Count > 0
                ? importedPlayset.ModIds
                : importedPlayset.EnabledModIds.ToList();
            existing.DisabledDlcIds = importedPlayset.DisabledDlcIds;
            existing.Source = importedPlayset.Source;
            existing.IsExternal = importedPlayset.IsExternal;
            existing.CanEdit = importedPlayset.CanEdit;
            _configuration.PlaysetStore.SavePlayset(existing);
        }
        SelectedPlayset = Playsets.FirstOrDefault(playset => playset.Id == _configuration.SelectedPlaysetId)
                          ?? Playsets.FirstOrDefault(playset => playset.IsExternal)
                          ?? SelectedPlayset;
        SetLocalizedStatusText("Paradox.Status.ImportedParadoxPlaysets", importedPlaysets.Count);
        OnPlaysetCollectionChanged();
    }

    [RelayCommand]
    private void MoveSelectedModUp()
    {
        MoveSelectedMod(-1);
    }

    [RelayCommand]
    private void MoveSelectedModDown()
    {
        MoveSelectedMod(1);
    }

    [RelayCommand]
    private void AddSelectedModToPlayset()
    {
        if (SelectedPlayset is null || SelectedAvailableMod is null || !EnsureSelectedPlaysetCanEdit())
        {
            return;
        }
        if (PlaysetMods.Any(mod => string.Equals(mod.Id, SelectedAvailableMod.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        var entry = new PlaysetModEntry(SelectedAvailableMod, true);
        entry.PropertyChanged += (_, _) =>
        {
            PersistCurrentPlaysetState();
            OnSelectionChanged();
        };
        PlaysetMods.Add(entry);
        SelectedPlaysetMod = entry;
        SelectedAvailableMod = null;
        PersistCurrentPlaysetState();
        RebuildAvailableMods();
        OnSelectionChanged();
        SetLocalizedStatusText("Paradox.Status.AddedToPlayset", entry.Title);
    }

    public void AddModToPlayset(ModEntry mod)
    {
        SelectedAvailableMod = mod;
        AddSelectedModToPlayset();
    }

    [RelayCommand]
    private void RemoveSelectedModFromPlayset()
    {
        if (SelectedPlaysetMod is null || !EnsureSelectedPlaysetCanEdit())
        {
            return;
        }
        var removed = SelectedPlaysetMod;
        PlaysetMods.Remove(removed);
        SelectedPlaysetMod = PlaysetMods.FirstOrDefault();
        PersistCurrentPlaysetState();
        RebuildAvailableMods();
        OnSelectionChanged();
        SetLocalizedStatusText("Paradox.Status.RemovedFromPlayset", removed.Title);
    }

    public void RemoveModFromPlayset(PlaysetModEntry mod)
    {
        SelectedPlaysetMod = mod;
        RemoveSelectedModFromPlayset();
    }

    [RelayCommand]
    private void RemovePlaysetMod(PlaysetModEntry mod)
    {
        RemoveModFromPlayset(mod);
    }

    public void MovePlaysetMod(PlaysetModEntry mod, int targetIndex)
    {
        if (!EnsureSelectedPlaysetCanEdit())
        {
            return;
        }

        var oldIndex = PlaysetMods.IndexOf(mod);
        if (oldIndex < 0 || PlaysetMods.Count <= 1)
        {
            return;
        }

        var boundedTargetIndex = Math.Clamp(targetIndex, 0, PlaysetMods.Count - 1);
        if (oldIndex == boundedTargetIndex)
        {
            return;
        }

        // Rebuild explicit order instead of relying only on Move semantics + filtered projection.
        var reordered = PlaysetMods.ToList();
        reordered.RemoveAt(oldIndex);
        reordered.Insert(boundedTargetIndex, mod);

        PlaysetMods.Clear();
        foreach (var entry in reordered)
        {
            PlaysetMods.Add(entry);
        }

        SelectedPlaysetMod = mod;
        RebuildFilteredPlaysetMods();
        PersistCurrentPlaysetState();
        OnSelectionChanged();
        SetLocalizedStatusText("Paradox.Status.ReorderedMod", mod.Title);
    }

    public void ReorderPlaysetMod(PlaysetModEntry mod, IReadOnlyList<PlaysetModEntry> orderedVisibleMods)
    {
        if (!EnsureSelectedPlaysetCanEdit() || orderedVisibleMods.Count == 0)
        {
            return;
        }

        var newVisibleOrder = orderedVisibleMods.ToList();
        if (newVisibleOrder.IndexOf(mod) < 0)
        {
            return;
        }

        List<PlaysetModEntry> reordered;
        if (string.IsNullOrWhiteSpace(PlaysetModSearchText)
            || FilteredPlaysetMods.Count == PlaysetMods.Count)
        {
            // Full list is visible: apply the visible order directly.
            if (newVisibleOrder.Count != PlaysetMods.Count
                || newVisibleOrder.Any(item => PlaysetMods.IndexOf(item) < 0))
            {
                return;
            }

            reordered = newVisibleOrder;
        }
        else
        {
            // Search is active: splice the reordered visible subset back into the full list
            // while preserving the relative positions of hidden items.
            var visibleSet = newVisibleOrder.ToHashSet();
            var queue = new Queue<PlaysetModEntry>(newVisibleOrder);
            reordered = new List<PlaysetModEntry>(PlaysetMods.Count);
            foreach (var entry in PlaysetMods)
            {
                if (visibleSet.Contains(entry))
                {
                    if (queue.Count > 0)
                    {
                        reordered.Add(queue.Dequeue());
                    }
                }
                else
                {
                    reordered.Add(entry);
                }
            }

            while (queue.Count > 0)
            {
                reordered.Add(queue.Dequeue());
            }
        }

        var unchanged = reordered.Count == PlaysetMods.Count
                        && reordered.Select((entry, index) => ReferenceEquals(entry, PlaysetMods[index])).All(equal => equal);
        if (unchanged)
        {
            return;
        }

        PlaysetMods.Clear();
        foreach (var entry in reordered)
        {
            PlaysetMods.Add(entry);
        }

        SelectedPlaysetMod = mod;
        RebuildFilteredPlaysetMods();
        PersistCurrentPlaysetState();
        OnSelectionChanged();
        SetLocalizedStatusText("Paradox.Status.ReorderedMod", mod.Title);
    }

    [RelayCommand]
    private void EnableAllPlaysetMods()
    {
        if (!EnsureSelectedPlaysetCanEdit())
        {
            return;
        }
        foreach (var mod in PlaysetMods)
        {
            mod.IsEnabled = true;
        }
        PersistCurrentPlaysetState();
        OnSelectionChanged();
    }

    [RelayCommand]
    private void DisableAllPlaysetMods()
    {
        if (!EnsureSelectedPlaysetCanEdit())
        {
            return;
        }
        foreach (var mod in PlaysetMods)
        {
            mod.IsEnabled = false;
        }
        PersistCurrentPlaysetState();
        OnSelectionChanged();
    }

    [RelayCommand]
    private void EnableAllDlcs()
    {
        foreach (var dlc in Dlcs)
        {
            dlc.IsEnabled = true;
        }
        OnSelectionChanged();
    }

    [RelayCommand]
    private void Launch()
    {
        try
        {
            SavePlayset();
            _service.StartGame();
            SetLocalizedStatusText("Paradox.Status.Launched");
            if (_configuration.CloseAfterLaunch)
            {
                _hostContext.ShutdownApplication();
            }
        }
        catch (Exception ex)
        {
            SetRawStatusText(ex.Message);
        }
    }

    [RelayCommand]
    private void OpenModLocation(object modLike)
    {
        if (TryGetModEntry(modLike) is not { } mod)
        {
            return;
        }
        try
        {
            _service.OpenModLocation(mod);
        }
        catch (Exception ex)
        {
            SetRawStatusText(ex.Message);
        }
    }

    [RelayCommand]
    private void OpenWorkshopPage(object modLike)
    {
        if (TryGetModEntry(modLike) is not { } mod || !mod.IsSteamWorkshopMod)
        {
            return;
        }
        try
        {
            _service.OpenWorkshopPage(mod);
        }
        catch (Exception ex)
        {
            SetRawStatusText(ex.Message);
        }
    }

    private void ApplySelectedPlaysetState()
    {
        if (SelectedPlayset is null)
        {
            PlaysetMods.Clear();
            AvailableMods.Clear();
            FilteredPlaysetMods.Clear();
            FilteredAvailableMods.Clear();
            return;
        }
        var playsetModIds = SelectedPlayset.ModIds.Count > 0
            ? SelectedPlayset.ModIds
            : SelectedPlayset.EnabledModIds;
        var enabledModIds = SelectedPlayset.EnabledModIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownMods = ParadoxModIdentity.BuildLookup(Mods);
        PlaysetMods.Clear();
        foreach (var modId in playsetModIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!knownMods.TryGetValue(modId, out var mod))
            {
                continue;
            }
            var entry = new PlaysetModEntry(mod, ParadoxModIdentity.ContainsModReference(enabledModIds, mod));
            entry.PropertyChanged += (_, _) =>
            {
                if (CanEditSelectedPlayset)
                {
                    PersistCurrentPlaysetState();
                    OnSelectionChanged();
                }
            };
            PlaysetMods.Add(entry);
        }
        foreach (var dlc in Dlcs)
        {
            dlc.IsEnabled = !SelectedPlayset.DisabledDlcIds.Contains(dlc.Id, StringComparer.OrdinalIgnoreCase);
        }
        SelectedPlaysetMod = PlaysetMods.FirstOrDefault();
        RebuildAvailableMods();
        OnSelectionChanged();
    }

    private void SaveHostPaths()
    {
        GameSettings.SaveLauncherOptions();
        _configuration.Save();
    }

    partial void OnModSearchTextChanged(string value)
    {
        RebuildFilteredMods();
    }

    partial void OnPlaysetModSearchTextChanged(string value)
    {
        RebuildFilteredPlaysetMods();
    }

    partial void OnAvailableModSearchTextChanged(string value)
    {
        RebuildFilteredAvailableMods();
    }

    public void ImportModFromArchive(string archivePath)
    {
        try
        {
            SaveHostPaths();
            var imported = _service.ImportModFromArchive(archivePath);
            Refresh();
            SelectedMod = Mods.FirstOrDefault(mod =>
                string.Equals(mod.Id, imported.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mod.DescriptorPath, imported.DescriptorPath, StringComparison.OrdinalIgnoreCase));
            SetLocalizedStatusText("Paradox.Status.ImportedMod", imported.Title);
        }
        catch (Exception ex)
        {
            SetRawStatusText(ex.Message);
        }
    }

    private void RebuildFilteredMods()
    {
        RebuildFilteredCollection(FilteredMods, Mods, ModSearchText, mod => mod);
    }

    private void RebuildFilteredPlaysetMods()
    {
        RebuildFilteredCollection(FilteredPlaysetMods, PlaysetMods, PlaysetModSearchText, mod => mod.Mod);
    }

    public void RefreshAvailableModFilters()
    {
        RebuildFilteredAvailableMods();
    }

    private void RebuildFilteredAvailableMods()
    {
        RebuildFilteredCollection(FilteredAvailableMods, AvailableMods, AvailableModSearchText, mod => mod);
    }

    private static void RebuildFilteredCollection<T>(
        ObservableCollection<T> target,
        IEnumerable<T> source,
        string? searchText,
        Func<T, ModEntry> modSelector)
    {
        var query = searchText?.Trim() ?? string.Empty;
        target.Clear();
        foreach (var item in source)
        {
            if (MatchesModSearch(modSelector(item), query))
            {
                target.Add(item);
            }
        }
    }

    private static bool MatchesModSearch(ModEntry mod, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }
        return ContainsIgnoreCase(mod.Title, query)
               || ContainsIgnoreCase(mod.Id, query)
               || ContainsIgnoreCase(mod.RemoteFileId, query)
               || ContainsIgnoreCase(mod.Version, query)
               || ContainsIgnoreCase(mod.SourceLabel, query)
               || ContainsIgnoreCase(mod.Subtitle, query)
               || ContainsIgnoreCase(mod.DescriptorPath, query)
               || ContainsIgnoreCase(mod.ContentPath, query);
    }

    private static bool ContainsIgnoreCase(string? value, string query)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void SetLocalizedStatusText(string key, params object[] args)
    {
        _statusTextKey = key;
        _statusTextArgs = args;
        StatusText = ParadoxGameLauncherStrings.Format(key, args);
    }

    private void SetRawStatusText(string text)
    {
        _statusTextKey = string.Empty;
        _statusTextArgs = [];
        StatusText = text;
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(PlaysetModCount));
        OnPropertyChanged(nameof(DisabledDlcCount));
        OnPropertyChanged(nameof(ExternalPlaysetCount));
        OnPropertyChanged(nameof(CanEditSelectedPlayset));
        OnPropertyChanged(nameof(SelectedPlaysetEditStateText));
        OnPropertyChanged(nameof(SelectedPlaysetSummaryText));
        DeletePlaysetCommand.NotifyCanExecuteChanged();
        AddSelectedModToPlaysetCommand.NotifyCanExecuteChanged();
        RemoveSelectedModFromPlaysetCommand.NotifyCanExecuteChanged();
        RemovePlaysetModCommand.NotifyCanExecuteChanged();
        MoveSelectedModUpCommand.NotifyCanExecuteChanged();
        MoveSelectedModDownCommand.NotifyCanExecuteChanged();
        EnableAllPlaysetModsCommand.NotifyCanExecuteChanged();
        DisableAllPlaysetModsCommand.NotifyCanExecuteChanged();
        EnableAllDlcsCommand.NotifyCanExecuteChanged();
    }

    private void OnPlaysetCollectionChanged()
    {
        OnPropertyChanged(nameof(PlaysetCount));
        OnPropertyChanged(nameof(ExternalPlaysetCount));
        OnPropertyChanged(nameof(ActivePlaysetName));
        OnPropertyChanged(nameof(CanEditSelectedPlayset));
        OnPropertyChanged(nameof(SelectedPlaysetEditStateText));
        OnPropertyChanged(nameof(SelectedPlaysetSummaryText));
    }

    private Playset CreateLocalPlayset(string name)
    {
        return new Playset
        {
            Name = name,
            ModIds = PlaysetMods.Select(mod => ParadoxModIdentity.GetStableId(mod.Mod)).ToList(),
            EnabledModIds = PlaysetMods.Where(mod => mod.IsEnabled)
                .Select(mod => ParadoxModIdentity.GetStableId(mod.Mod))
                .ToList(),
            DisabledDlcIds = Dlcs.Where(dlc => !dlc.IsEnabled).Select(dlc => dlc.Id).ToList(),
            Source = "Shadow",
            CanEdit = true,
        };
    }

    private void PersistCurrentPlaysetState()
    {
        if (SelectedPlayset is null || !SelectedPlayset.CanEdit)
        {
            return;
        }
        SelectedPlayset.ModIds = PlaysetMods
            .Select(mod => ParadoxModIdentity.GetStableId(mod.Mod))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        SelectedPlayset.EnabledModIds = PlaysetMods
            .Where(mod => mod.IsEnabled)
            .Select(mod => ParadoxModIdentity.GetStableId(mod.Mod))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        SelectedPlayset.DisabledDlcIds = Dlcs
            .Where(dlc => !dlc.IsEnabled)
            .Select(dlc => dlc.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _configuration.PlaysetStore.SavePlayset(SelectedPlayset);
    }

    private void RebuildAvailableMods()
    {
        var playsetModIds = PlaysetMods
            .Select(mod => mod.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var previouslySelectedId = SelectedAvailableMod?.Id;
        AvailableMods.Clear();
        foreach (var mod in Mods
                     .Where(mod => !playsetModIds.Contains(mod.Id))
                     .OrderBy(mod => mod.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            AvailableMods.Add(mod);
        }
        SelectedAvailableMod = AvailableMods.FirstOrDefault(mod =>
                                   string.Equals(mod.Id, previouslySelectedId, StringComparison.OrdinalIgnoreCase))
                               ?? AvailableMods.FirstOrDefault();
        RebuildFilteredAvailableMods();
        RebuildFilteredPlaysetMods();
    }

    private void NotifyLauncherOptionProperties()
    {
        OnPropertyChanged(nameof(GameExecutablePath));
        OnPropertyChanged(nameof(GameUserDirectory));
        OnPropertyChanged(nameof(WorkshopDirectory));
        OnPropertyChanged(nameof(LaunchArguments));
        OnPropertyChanged(nameof(CloseAfterLaunch));
    }

    private void MoveSelectedMod(int direction)
    {
        if (SelectedPlaysetMod is null || !EnsureSelectedPlaysetCanEdit())
        {
            return;
        }
        var oldIndex = PlaysetMods.IndexOf(SelectedPlaysetMod);
        var newIndex = oldIndex + direction;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= PlaysetMods.Count)
        {
            return;
        }
        PlaysetMods.Move(oldIndex, newIndex);
        RebuildFilteredPlaysetMods();
        PersistCurrentPlaysetState();
        OnSelectionChanged();
    }

    private void ReloadStoredPlaysets()
    {
        var previousSelectedId = SelectedPlayset?.Id ?? _configuration.SelectedPlaysetId;
        var playsets = _configuration.PlaysetStore.LoadPlaysets();
        if (playsets.Count == 0)
        {
            var defaultPlayset = Playset.CreateDefault();
            _configuration.PlaysetStore.SavePlayset(defaultPlayset);
            playsets = [defaultPlayset];
        }
        Playsets.Clear();
        foreach (var playset in playsets)
        {
            Playsets.Add(playset);
        }
        SelectedPlayset = Playsets.FirstOrDefault(playset => playset.Id == previousSelectedId)
                          ?? Playsets.FirstOrDefault(playset => playset.Id == _configuration.SelectedPlaysetId)
                          ?? Playsets.FirstOrDefault();
        OnPlaysetCollectionChanged();
    }

    private bool EnsureSelectedPlaysetCanEdit()
    {
        if (CanEditSelectedPlayset)
        {
            return true;
        }
        if (SelectedPlayset is not null)
        {
            SetLocalizedStatusText("Paradox.Status.CannotEditPlayset", SelectedPlayset.Name);
        }
        return false;
    }

    private static ModEntry? TryGetModEntry(object modLike)
    {
        return modLike switch
        {
            ModEntry mod => mod,
            PlaysetModEntry playsetMod => playsetMod.Mod,
            _ => null,
        };
    }
}
