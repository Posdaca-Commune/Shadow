using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;
using Shadow.ParadoxGameLauncher.Localization;

namespace Shadow.ParadoxGameLauncher.Models;

public sealed partial class Playset : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string _name = ParadoxGameLauncherStrings.Get("Paradox.Playset.New");

    [ObservableProperty] private List<string> _enabledModIds = [];

    [ObservableProperty] private List<string> _modIds = [];

    [ObservableProperty] private List<string> _disabledDlcIds = [];

    [ObservableProperty] private string _source = "Shadow";

    [ObservableProperty] private bool _isExternal;

    [ObservableProperty]
    [JsonPropertyName("can_edit")]
    private bool _canEdit = true;

    [JsonIgnore] public string StorageFilePath { get; set; } = string.Empty;

    public static Playset CreateDefault()
    {
        return new Playset
        {
            Id = "default",
            Name = ParadoxGameLauncherStrings.Get("Paradox.Playset.Default"),
        };
    }
}
