using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TempoCue.Application;

namespace TempoCue.App.Persistence;

/// <summary>Владеет локальной JSON-схемой состояния Coach Pack, не зная пути к файлу.</summary>
internal static class LocalCoachPackStateSerializer
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static async Task<StoredCoachPackState> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var metadata = await JsonSerializer.DeserializeAsync<LocalCoachPackMetadata>(
            stream,
            SerializerOptions,
            cancellationToken);
        if (metadata is null
            || metadata.SchemaVersion != CurrentSchemaVersion
            || string.IsNullOrWhiteSpace(metadata.PackId)
            || !Enum.IsDefined(metadata.State))
        {
            throw new InvalidDataException("Local Coach Pack state is invalid.");
        }

        return new StoredCoachPackState(metadata.PackId, metadata.State);
    }

    public static Task WriteAsync(
        Stream stream,
        string packId,
        LocalCoachPackState state,
        DateTimeOffset savedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        var metadata = new LocalCoachPackMetadata(
            CurrentSchemaVersion,
            packId,
            state,
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
        DateTimeOffset SavedAtUtc);
}

internal sealed record StoredCoachPackState(
    string PackId,
    LocalCoachPackState State);
