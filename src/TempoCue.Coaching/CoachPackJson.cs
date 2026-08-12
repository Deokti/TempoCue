using System.Text.Json;
using System.Text.Json.Serialization;

namespace TempoCue.Coaching;

/// <summary>Единые настройки JSON для чтения и записи Coach Pack.</summary>
internal static class CoachPackJson
{
    internal static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
