using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Shadow.Abstractions;
using Shadow.Hoi4Launcher.Models;
using Shadow.Hoi4Launcher.Services;

namespace Shadow.Hoi4Launcher.ViewModels;

public sealed partial class Hoi4LauncherViewModel : ObservableObject
{
    private readonly Hoi4LauncherConfiguration _configuration;
    private readonly Hoi4LauncherService _service;
    private readonly IShadowHostContext _hostContext;

    public Hoi4LauncherViewModel(
        Hoi4LauncherConfiguration configuration,
        Hoi4LauncherService service,
        IShadowHostContext hostContext)
    {
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

        Refresh();
    }

    public IReadOnlyList<LauncherSection> Sections { get; } =
    [
        new("Home", "启动首页", FASymbol.Home),
        new("Mods", "Mod 列表", FASymbol.Library),
        new("Dlcs", "DLC 列表", FASymbol.Shop),
        new("Playsets", "播放集列表", FASymbol.BulletList),
        new("GameSettings", "游戏设置", FASymbol.Setting),
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

    [ObservableProperty] private string _statusText = "选择游戏路径后刷新，可直接启动游戏。";

    public int EnabledModCount => PlaysetMods.Count(mod => mod.IsEnabled);

    public int PlaysetModCount => PlaysetMods.Count;

    public int DisabledDlcCount => Dlcs.Count(dlc => !dlc.IsEnabled);

    public int ExternalPlaysetCount => Playsets.Count(playset => playset.IsExternal);

    public int PlaysetCount => Playsets.Count;

    public string ActivePlaysetName => SelectedPlayset?.Name ?? "未选择播放集";

    public bool CanEditSelectedPlayset => SelectedPlayset?.CanEdit == true;

    public string SelectedPlaysetEditStateText => SelectedPlayset?.CanEdit == false ? "只读播放集" : "可编辑播放集";

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
            StatusText = $"Mod 索引写入失败：{ex.Message}";
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
        StatusText = $"已发现 {Mods.Count} 个 mod，{Dlcs.Count} 个 DLC。";
    }

    [RelayCommand]
    private void AddPlayset()
    {
        var name = string.IsNullOrWhiteSpace(NewPlaysetName)
            ? "新播放集"
            : NewPlaysetName.Trim();

        var playsetName = SelectedPlayset?.CanEdit == false
            ? $"{SelectedPlayset.Name} 副本"
            : name;
        var playset = CreateLocalPlayset(playsetName);

        Playsets.Add(playset);
        _configuration.PlaysetStore.SavePlayset(playset);
        SelectedPlayset = playset;
        NewPlaysetName = string.Empty;
        _configuration.Save();
        OnPlaysetCollectionChanged();
        StatusText = $"已创建播放集：{playset.Name}";
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
            StatusText = $"播放集不可删除：{playset.Name}";
            return;
        }

        if (Playsets.Count <= 1)
        {
            StatusText = "至少需要保留一个播放集。";
            return;
        }

        var index = Playsets.IndexOf(playset);
        Playsets.Remove(playset);
        _configuration.PlaysetStore.DeletePlayset(playset);

        SelectedPlayset = Playsets.ElementAtOrDefault(Math.Clamp(index, 0, Playsets.Count - 1))
                          ?? Playsets.FirstOrDefault();
        _configuration.Save();
        OnPlaysetCollectionChanged();
        StatusText = $"已删除播放集：{playset.Name}";
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
            StatusText = $"已应用只读播放集：{SelectedPlayset.Name}";
            return;
        }

        PersistCurrentPlaysetState();
        _service.ApplyPlayset(SelectedPlayset, PlaysetMods.Select(mod => mod.Mod), Dlcs);
        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(PlaysetModCount));
        OnPropertyChanged(nameof(DisabledDlcCount));
        StatusText = $"已保存并应用播放集：{SelectedPlayset.Name}";
    }

    [RelayCommand]
    private void ImportParadoxPlaysets()
    {
        SaveHostPaths();
        var importedPlaysets = _service.ImportParadoxPlaysets(Mods);
        if (importedPlaysets.Count == 0)
        {
            StatusText = "未发现 Paradox 启动器播放集。";
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
        StatusText = $"已读取 {importedPlaysets.Count} 个 Paradox 播放集。";
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
        StatusText = $"已添加到播放集：{entry.Title}";
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
        StatusText = $"已从播放集移除：{removed.Title}";
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
        StatusText = $"已调整加载顺序：{mod.Title}";
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
        if (!EnsureSelectedPlaysetCanEdit())
        {
            return;
        }

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
            StatusText = "HOI4 已启动。";
            if (_configuration.State.CloseAfterLaunch)
            {
                _hostContext.ShutdownApplication();
            }
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
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
            StatusText = ex.Message;
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
            StatusText = ex.Message;
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
        var knownMods = Mods
            .GroupBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        PlaysetMods.Clear();
        foreach (var modId in playsetModIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!knownMods.TryGetValue(modId, out var mod))
            {
                continue;
            }

            var entry = new PlaysetModEntry(mod, enabledModIds.Contains(mod.Id));
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

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(EnabledModCount));
        OnPropertyChanged(nameof(PlaysetModCount));
        OnPropertyChanged(nameof(DisabledDlcCount));
        OnPropertyChanged(nameof(ExternalPlaysetCount));
        OnPropertyChanged(nameof(CanEditSelectedPlayset));
        OnPropertyChanged(nameof(SelectedPlaysetEditStateText));
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
    }

    private Playset CreateLocalPlayset(string name)
    {
        return new Playset
        {
            Name = name,
            ModIds = PlaysetMods.Select(mod => mod.Id).ToList(),
            EnabledModIds = PlaysetMods.Where(mod => mod.IsEnabled).Select(mod => mod.Id).ToList(),
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
            .Select(mod => mod.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        SelectedPlayset.EnabledModIds = PlaysetMods
            .Where(mod => mod.IsEnabled)
            .Select(mod => mod.Id)
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
            StatusText = $"播放集不可编辑：{SelectedPlayset.Name}";
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