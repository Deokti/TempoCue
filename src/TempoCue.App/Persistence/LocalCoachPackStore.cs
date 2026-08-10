using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Persistence;

/// <summary>Хранит пользовательские Coach Packs и их локальное состояние в LocalAppData.</summary>
public sealed class LocalCoachPackStore : ICoachPackStore
{
    private static readonly JsonSerializerOptions StateSerializerOptions = CreateStateOptions();
    private readonly string _rootDirectory;

    public LocalCoachPackStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    public async Task<CoachPackSaveResult> SaveAsync(
        CoachPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        CoachPackValidator.Validate(pack);

        Directory.CreateDirectory(_rootDirectory);
        var fileStem = CreateSafeFileStem(pack.Id);
        var packPath = Path.Combine(_rootDirectory, $"{fileStem}.json");
        var statePath = Path.Combine(_rootDirectory, $"{fileStem}.state.json");
        var packTemporaryPath = CreateTemporaryPath(packPath);
        var stateTemporaryPath = CreateTemporaryPath(statePath);

        try
        {
            await using (var packStream = CreateFile(packTemporaryPath))
            {
                await CoachPackWriter.WriteAsync(packStream, pack, cancellationToken);
            }

            var metadata = new LocalCoachPackMetadata(
                1,
                pack.Id,
                state,
                DateTimeOffset.UtcNow);
            await using (var stateStream = CreateFile(stateTemporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stateStream,
                    metadata,
                    StateSerializerOptions,
                    cancellationToken);
            }

            File.Move(packTemporaryPath, packPath, true);
            File.Move(stateTemporaryPath, statePath, true);
            return new CoachPackSaveResult(pack.Id, state, packPath);
        }
        finally
        {
            DeleteTemporaryFile(packTemporaryPath);
            DeleteTemporaryFile(stateTemporaryPath);
        }
    }

    private static FileStream CreateFile(string path) => new(
        path,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None,
        4096,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static string CreateSafeFileStem(string packId)
    {
        var safeCharacters = packId
            .Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '-')
            .ToArray();
        var result = new string(safeCharacters).Trim('.', '-', '_');
        return result.Length == 0 ? "coach-pack" : result;
    }

    private static string CreateTemporaryPath(string destinationPath) =>
        $"{destinationPath}.{Guid.NewGuid():N}.tmp";

    private static void DeleteTemporaryFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static JsonSerializerOptions CreateStateOptions()
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
