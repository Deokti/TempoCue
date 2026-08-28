using System.Text.Json;
using System.Text.Json.Serialization;

namespace TempoCue.Vision;

/// <summary>
/// Читает и записывает ручную разметку replay-кадра. Разметка хранится в
/// сопутствующем JSON-файле рядом с кадром и не меняет формат `.tempocue-vision`.
/// </summary>
public static class MinimapReplayGroundTruthStore
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Суффикс сопутствующего файла рядом с кадром `.tempocue-vision`.</summary>
    public const string SidecarSuffix = ".labels.json";

    private const int MaximumDocumentLength = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task SaveAsync(
        Stream destination,
        MinimapReplayGroundTruth groundTruth,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(groundTruth);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("Поток должен поддерживать запись.", nameof(destination));
        }

        var document = new GroundTruthDocument(
            CurrentSchemaVersion,
            groundTruth.Regions
                .Select(static region => new RegionDocument(
                    region.X,
                    region.Y,
                    region.Width,
                    region.Height,
                    region.Kind,
                    region.ChampionId,
                    region.Affiliation))
                .ToArray(),
            groundTruth.AllowedFalsePositives,
            groundTruth.AllowedMissedChampions);
        await JsonSerializer.SerializeAsync(
            destination,
            document,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<MinimapReplayGroundTruth> LoadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("Поток должен поддерживать чтение.", nameof(source));
        }

        if (source.CanSeek && source.Length > MaximumDocumentLength)
        {
            throw new InvalidDataException("Размер файла разметки некорректен.");
        }

        GroundTruthDocument? document;
        try
        {
            document = await JsonSerializer.DeserializeAsync<GroundTruthDocument>(
                source,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Файл разметки не является корректным JSON.", exception);
        }

        if (document is null)
        {
            throw new InvalidDataException("Файл разметки пуст.");
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Версия разметки {document.SchemaVersion} не поддерживается.");
        }

        if (document.Regions is null)
        {
            throw new InvalidDataException("Файл разметки не содержит списка областей.");
        }

        try
        {
            return new MinimapReplayGroundTruth(
                document.Regions
                    .Select(static region => new MinimapGroundTruthRegion(
                        region.X,
                        region.Y,
                        region.Width,
                        region.Height,
                        region.Kind,
                        region.ChampionId,
                        region.Affiliation))
                    .ToArray(),
                document.AllowedFalsePositives,
                document.AllowedMissedChampions);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Описание размеченной области некорректно.", exception);
        }
    }

    private sealed record GroundTruthDocument(
        int SchemaVersion,
        RegionDocument[]? Regions,
        int AllowedFalsePositives,
        int AllowedMissedChampions);

    private sealed record RegionDocument(
        int X,
        int Y,
        int Width,
        int Height,
        MinimapGroundTruthKind Kind,
        string? ChampionId,
        MinimapMarkerAffiliation? Affiliation);
}
