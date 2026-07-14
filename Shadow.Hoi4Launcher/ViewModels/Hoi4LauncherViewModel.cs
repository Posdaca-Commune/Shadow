using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;
using Shadow.Hoi4Launcher.Localization;
using Shadow.Hoi4Launcher.Models;
using Shadow.Hoi4Launcher.Services;

namespace Shadow.Hoi4Launcher.ViewModels;

public sealed partial class Hoi4LauncherViewModel : ObservableObject
{
    private readonly Hoi4LauncherConfiguration _configuration;
    private readonly Hoi4LauncherService _service;
    private readonly IShadowHostContext _hostContext;
    private string _statusTextKey = "Hoi4.Status.Default";
    private object[] _statusTextArgs = [];

    public Hoi4LauncherViewModel(
        Hoi4LauncherConfiguration configuration,
        Hoi4LauncherService service,
        IShadowHostContext hostContext)
    {
        Hoi4LauncherStrings.Register();
        _configuration = configuration;
        _service = service;
        _hostContext = hostContext;
        GameSettings = new Hoi4GameSettingsViewModel(configuration, service);
        GameSettings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Hoi4GameSettingsViewModel.GameExecutablePath)
                or nameof(Hoi4GameSettingsViewModel.GameUserDirectory)
                or nameof(Hoi4GameSettingsViewModel.WorkshopDirectory)
                or nameof(Hoi4GameSettingsViewModel.LaunchArguments)
                or nameof(Hoi4GameSettingsViewModel.CloseAfterLaunch))
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
                StatusText = Hoi4LauncherStrings.Format(_statusTextKey, _statusTextArgs);
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
        new("Home", LocalizedText.Key("Hoi4.Section.Home.Title"),
            LocalizedText.Key("Hoi4.Section.Home.Description"), FASymbol.Home),
        new("Mods", LocalizedText.Key("Hoi4.Section.Mods.Title"),
            LocalizedText.Key("Hoi4.Section.Mods.Description"), FASymbol.Library),
        new("Dlcs", LocalizedText.Key("Hoi4.Section.Dlcs.Title"),
            LocalizedText.Key("Hoi4.Section.Dlcs.Description"), FASymbol.Shop),
        new("Playsets", LocalizedText.Key("Hoi4.Section.Playsets.Title"),
            LocalizedText.Key("Hoi4.Section.Playsets.Description"), FASymbol.BulletList),
        new("GameSettings", LocalizedText.Key("Hoi4.Section.GameSettings.Title"),
            LocalizedText.Key("Hoi4.Section.GameSettings.Description"), FASymbol.Setting),
    ];

    public ObservableCollection<ModEntry> Mods { get; } = [];

    public ObservableCollection<PlaysetModEntry> PlaysetMods { get; } = [];

    public ObservableCollection<ModEntry> AvailableMods { get; } = [];

    public ObservableCollection<DlcEntry> Dlcs { get; } = [];

    public ObservableCollection<Playset> Playsets { get; } = [];

    public Hoi4GameSettingsViewModel GameSettings { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSelected))]
    [NotifyPropertyChangedFor(nameof(IsModsSelected))]
    [NotifyPropertyChangedFor(nameof(IsDlcsSelected))]
    [NotifyPropertyChangedFor(nameof(IsPlaysetsSelected))]
    [NotifyPropertyChangedFor(nameof(IsGameSettingsSelected))]
    private LauncherSection _selectedSection = null!;

    [ObservableProperty] private Playset? _selectedPlayset;

    [ObservableProperty] private ModEntry? _selectedMod;

    [ObservableProperty] private PlaysetModEntry? _selectedPlaysetMod;

    [ObservableProperty] private ModEntry? _selectedAvailableMod;

    [ObservableProperty] private string _newPlaysetName = string.Empty;

    [ObservableProperty] private string _statusText = Hoi4LauncherStrings.Get("Hoi4.Status.Default");

    public int EnabledModCount => PlaysetMods.Count(mod => mod.IsEnabled);

    public int PlaysetModCount => PlaysetMods.Count;

    public int DisabledDlcCount => Dlcs.Count(dlc => !dlc.IsEnabled);

    public int ExternalPlaysetCount => Playsets.Count(playset => playset.IsExternal);

    public int PlaysetCount => Playsets.Count;

    public string ActivePlaysetName => SelectedPlayset?.Name ?? Hoi4LauncherStrings.Get("Hoi4.Playsets.NoSelection");

    public bool CanEditSelectedPlayset => SelectedPlayset?.CanEdit == true;

    public string SelectedPlaysetEditStateText => SelectedPlayset?.CanEdit == false
        ? Hoi4LauncherStrings.Get("Hoi4.Playsets.ReadOnly")
        : Hoi4LauncherStrings.Get("Hoi4.Playsets.Editable");

    public string SelectedPlaysetSummaryText => Hoi4LauncherStrings.Format(
        "Hoi4.Playsets.Summary",
        PlaysetModCount,
        EnabledModCount,
        SelectedPlaysetEditStateText);

    public string SelectedPlaysetStorageDirectory =>
        Path.Combine(_configuration.PlaysetStore.WorkspaceDirectory, "playsets");

    public string GameExecutablePath => _configuration.State.GameExecutablePath;

    public string GameUserDirectory => _configuration.State.GameUserDirectory;

    public string WorkshopDirectory => _configuration.State.WorkshopDirectory;

    public string LaunchArguments => _configuration.State.LaunchArguments;

    public bool CloseAfterLaunch => _configuration.State.CloseAfterLaunch;

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

    partial void OnSelectedPlaysetChanged(Playset? value)
    {
        if (value is null)
        {
            return;
        }

        _configuration.State.SelectedPlaysetId = value.Id;
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
            SetLocalizedStatusText("Hoi4.Status.ModIndexFailed", ex.Message);
        }

        Dlcs.Clear();
        foreach (var dlc in _service.DiscoverDlcs())
        {
            dlc.PropertyChanged += (_, _) => OnSelectionChanged();
            Dlcs.Add(dlc);
        }

        ApplySelectedPlaysetState();
        RebuildAvailableMods();
        NotifyLauncherOptionProperties();
        SetLocalizedStatusText("Hoi4.Status.Refreshed", Mods.Count, Dlcs.Count);
    }

    [RelayCommand]
    private void AddPlayset()
    {
        var name = string.IsNullOrWhiteSpace(NewPlaysetName)
            ? Hoi4LauncherStrings.Get("Hoi4.Playset.New")
            : NewPlaysetName.Trim();

        var playsetName = SelectedPlayset?.CanEdit == false
            ? Hoi4LauncherStrings.Format("Hoi4.Playset.CopySuffix", SelectedPlayset.Name)
            : name;
        var playset = CreateLocalPlayset(playsetName);

        Playsets.Add(playset);
        _configuration.PlaysetStore.SavePlayset(playset);
        SelectedPlayset = playset;
        NewPlaysetName = string.Empty;
        _configuration.Save();
        OnPlaysetCollectionChanged();
        SetLocalizedStatusText("Hoi4.Status.CreatedPlayset", playset.Name);
    }

    public void AddPlayset(string name)
    {
        NewPlaysetName = name;
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
            SetLocalizedStatusText("Hoi4.Status.CannotDeletePlayset", playset.Name);
            return;
        }

        if (Playsets.Count <= 1)
        {
            SetLocalizedStatusText("Hoi4.Status.NeedOnePlayset");
            return;
        }

        var index = Playsets.IndexOf(playset);
        Playsets.Remove(playset);
        _configuration.PlaysetStore.DeletePlayset(playset);

        SelectedPlayset = Playsets.ElementAtOrDefault(Math.Clamp(index, 0, Playsets.Count - 1))
                          ?? Playsets.FirstOrDefault();
        _configuration.Save();
        OnPlaysetCollectionChanged();
        SetLocalizedStatusText("Hoi4.Status.DeletedPlayset", playset.Name);
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
            SetLocalizedStatusText("Hoi4.Status.AppliedReadOnlyPlayset", SelectedPlayset.Name);
            return;
        }

        PersistCurrentPlaysetState();
        _service.ApplyPlayset(SelectedPlayset, PlaysetMods.Select(mod => mod.Mod), Dlcs);
        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(PlaysetModCount));
        OnPropertyChanged(nameof(DisabledDlcCount));
        SetLocalizedStatusText("Hoi4.Status.SavedAppliedPlayset", SelectedPlayset.Name);
    }

    [RelayCommand]
    private void ImportParadoxPlaysets()
    {
        SaveHostPaths();
        var importedPlaysets = _service.ImportParadoxPlaysets(Mods);
        if (importedPlaysets.Count == 0)
        {
            SetLocalizedStatusText("Hoi4.Status.NoParadoxPlaysets");
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

        SelectedPlayset = Playsets.FirstOrDefault(playset => playset.Id == _configuration.State.SelectedPlaysetId)
                          ?? Playsets.FirstOrDefault(playset => playset.IsExternal)
                          ?? SelectedPlayset;
        SetLocalizedStatusText("Hoi4.Status.ImportedParadoxPlaysets", importedPlaysets.Count);
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
        SetLocalizedStatusText("Hoi4.Status.AddedToPlayset", entry.Title);
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
        SetLocalizedStatusText("Hoi4.Status.RemovedFromPlayset", removed.Title);
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
        if (oldIndex < 0)
        {
            return;
        }

        var boundedTargetIndex = Math.Clamp(targetIndex, 0, PlaysetMods.Count - 1);
        if (oldIndex == boundedTargetIndex)
        {
            return;
        }

        PlaysetMods.Move(oldIndex, boundedTargetIndex);
        SelectedPlaysetMod = mod;
        PersistCurrentPlaysetState();
        SetLocalizedStatusText("Hoi4.Status.ReorderedMod", mod.Title);
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
            SetLocalizedStatusText("Hoi4.Status.Launched");
            if (_configuration.State.CloseAfterLaunch)
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
            return;
        }

        var playsetModIds = SelectedPlayset.ModIds.Count > 0
            ? SelectedPlayset.ModIds
            : SelectedPlayset.EnabledModIds;
        var enabledModIds = SelectedPlayset.EnabledModIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownMods = Hoi4ModIdentity.BuildLookup(Mods);

        PlaysetMods.Clear();
        foreach (var modId in playsetModIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!knownMods.TryGetValue(modId, out var mod))
            {
                continue;
            }

            var entry = new PlaysetModEntry(mod, Hoi4ModIdentity.ContainsModReference(enabledModIds, mod));
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

    private void SetLocalizedStatusText(string key, params object[] args)
    {
        _statusTextKey = key;
        _statusTextArgs = args;
        StatusText = Hoi4LauncherStrings.Format(key, args);
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
            ModIds = PlaysetMods.Select(mod => Hoi4ModIdentity.GetStableId(mod.Mod)).ToList(),
            EnabledModIds = PlaysetMods.Where(mod => mod.IsEnabled)
                .Select(mod => Hoi4ModIdentity.GetStableId(mod.Mod))
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
            .Select(mod => Hoi4ModIdentity.GetStableId(mod.Mod))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        SelectedPlayset.EnabledModIds = PlaysetMods
            .Where(mod => mod.IsEnabled)
            .Select(mod => Hoi4ModIdentity.GetStableId(mod.Mod))
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
        PersistCurrentPlaysetState();
    }

    private void ReloadStoredPlaysets()
    {
        var previousSelectedId = SelectedPlayset?.Id ?? _configuration.State.SelectedPlaysetId;
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
                          ?? Playsets.FirstOrDefault(playset => playset.Id == _configuration.State.SelectedPlaysetId)
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
            SetLocalizedStatusText("Hoi4.Status.CannotEditPlayset", SelectedPlayset.Name);
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





