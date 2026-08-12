using System.Text.Json;

namespace TempoCue.Coaching;

/// <summary>Загружает и проверяет Map Control Pack из переносимого JSON.</summary>
public static class MapControlPackLoader
{
    public static MapControlPack Load(Stream jsonStream)
    {
        ArgumentNullException.ThrowIfNull(jsonStream);

        var pack = JsonSerializer.Deserialize<MapControlPack>(jsonStream, CoachPackJson.Options)
            ?? throw new InvalidDataException("Map Control Pack is empty.");
        MapControlPackValidator.Validate(pack);
        return pack;
    }

    public static async Task<MapControlPack> LoadAsync(
        Stream jsonStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsonStream);

        var pack = await JsonSerializer.DeserializeAsync<MapControlPack>(
            jsonStream,
            CoachPackJson.Options,
            cancellationToken) ?? throw new InvalidDataException("Map Control Pack is empty.");
        MapControlPackValidator.Validate(pack);
        return pack;
    }
}
