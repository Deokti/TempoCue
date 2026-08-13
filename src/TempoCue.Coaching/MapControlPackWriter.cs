using System.Text.Json;

namespace TempoCue.Coaching;

/// <summary>Записывает проверенный Map Control Pack в переносимый JSON.</summary>
public static class MapControlPackWriter
{
    public static async Task WriteAsync(
        Stream destination,
        MapControlPack pack,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(pack);
        MapControlPackValidator.Validate(pack);
        await JsonSerializer.SerializeAsync(
            destination,
            pack,
            CoachPackJson.Options,
            cancellationToken);
    }
}
