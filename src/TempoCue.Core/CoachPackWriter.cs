using System.Text.Json;

namespace TempoCue.Core;

/// <summary>Записывает проверенный Coach Pack в переносимый JSON-поток.</summary>
public static class CoachPackWriter
{
    public static async Task WriteAsync(
        Stream destination,
        CoachPack pack,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(pack);
        CoachPackValidator.Validate(pack);
        await JsonSerializer.SerializeAsync(
            destination,
            pack,
            CoachPackJson.Options,
            cancellationToken);
    }
}
