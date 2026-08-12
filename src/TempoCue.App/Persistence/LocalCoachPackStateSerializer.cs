using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TempoCue.Application;

namespace TempoCue.App.Persistence;

/// <summary>Владеет локальной JSON-схемой состояния Coach Pack, не зная пути к файлу.</summary>
internal static class LocalCoachingPackStateSerializer
{
    private const int CurrentSchemaVersion = 2;
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static async Task<StoredCoachingPackState> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var metadata = await JsonSerializer.DeserializeAsync<LocalCoachPackMetadata>(
            stream,
            SerializerOptions,
            cancellationToken);
        var origin = metadata?.SchemaVersion == 1
            ? LocalCoachPackOrigin.CreatedByUser
            : metadata?.Origin;
        if (metadata is null
            || metadata.SchemaVersion is not (1 or CurrentSchemaVersion)
            || string.IsNullOrWhiteSpace(metadata.PackId)
            || !Enum.IsDefined(metadata.State)
            || origin is null
            || !Enum.IsDefined(origin.Value))
        {
            throw new InvalidDataException("Local Coach Pack state is invalid.");
        }

        return new StoredCoachingPackState(metadata.PackId, metadata.State, origin.Value);
    }

    public static Task WriteAsync(
        Stream stream,
        string packId,
        LocalCoachPackState state,
        LocalCoachPackOrigin origin,
        DateTimeOffset savedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        var metadata = new LocalCoachPackMetadata(
            CurrentSchemaVersion,
            packId,
            state,
            origin,
            savedAtUtc);
        return JsonSerializer.SerializeAsync(
            stream,
            metadata,
            SerializerOptions,
            cancellationToken);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed record LocalCoachPackMetadata(
        int SchemaVersion,
        string PackId,
        LocalCoachPackState State,
        LocalCoachPackOrigin? Origin,
        DateTimeOffset SavedAtUtc);
}

internal sealed record StoredCoachingPackState(
    string PackId,
    LocalCoachPackState State,
    LocalCoachPackOrigin Origin);
