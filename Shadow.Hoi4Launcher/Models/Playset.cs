using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace Shadow.Hoi4Launcher.Models;

public sealed partial class Playset : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string _name = "新播放集";

    [ObservableProperty] private List<string> _enabledModIds = [];

    [ObservableProperty] private List<string> _modIds = [];

    [ObservableProperty] private List<string> _disabledDlcIds = [];

    [ObservableProperty] private string _source = "Shadow";

    [ObservableProperty] private bool _isExternal;

    [ObservableProperty] [property: JsonPropertyName("can_edit")]
    private bool _canEdit = true;

    [JsonIgnore] public string StorageFilePath { get; set; } = string.Empty;

    public static Playset CreateDefault()
    {
        return new Playset
        {
            Id = "default",
            Name = "默认播放集",
        };
    }
}