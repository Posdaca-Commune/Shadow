using System.Text.Json.Serialization;

namespace Shadow.ParadoxGameLauncher.Services;

internal sealed class DlcLoadDocument
{
    [JsonPropertyName("enabled_mods")]
    public List<string> EnabledMods { get; set; } = [];

    [JsonPropertyName("disabled_dlcs")]
    public List<string> DisabledDlcs { get; set; } = [];
}
