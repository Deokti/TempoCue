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

    public async Task<CoachPackLibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return new CoachPackLibrarySnapshot(
                [],
                0,
                new Dictionary<string, LocalCoachPackState>(StringComparer.OrdinalIgnoreCase));
        }

        var entries = new List<LocalCoachPackEntry>();
        var invalidFileCount = 0;
        var localStates = new Dictionary<string, LocalStateReadResult>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var statePath in Directory.EnumerateFiles(
                     _rootDirectory,
                     "*.state.json",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stateResult = await LoadStateFileAsync(statePath, cancellationToken);
                localStates[stateResult.PackId] = stateResult;
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or InvalidDataException
                                              or JsonException)
            {
                invalidFileCount++;
            }
        }

        foreach (var packPath in Directory
                     .EnumerateFiles(_rootDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     .Where(static path => !path.EndsWith(
                         ".state.json",
                         StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var packStream = OpenFile(packPath);
                var pack = await CoachPackLoader.LoadAsync(packStream, cancellationToken);
                var stateResult = localStates.GetValueOrDefault(pack.Id);
                entries.Add(new LocalCoachPackEntry(
                    pack,
                    stateResult?.State ?? LocalCoachPackState.Draft,
                    File.GetLastWriteTimeUtc(packPath)));
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or InvalidDataException
                                              or JsonException)
            {
                invalidFileCount++;
            }
        }

        return new CoachPackLibrarySnapshot(
            entries
                .OrderByDescending(static entry => entry.SavedAtUtc)
                .ThenBy(static entry => entry.Pack.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            invalidFileCount,
            localStates.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.State,
                StringComparer.OrdinalIgnoreCase));
    }

    public async Task<CoachPackSaveResult> SaveAsync(
        CoachPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ValidateState(state);
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

    public async Task SetStateAsync(
        string packId,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        ValidateState(state);
        Directory.CreateDirectory(_rootDirectory);
        var fileStem = CreateSafeFileStem(packId);
        var statePath = Path.Combine(_rootDirectory, $"{fileStem}.state.json");
        var temporaryPath = CreateTemporaryPath(statePath);

        try
        {
            var metadata = new LocalCoachPackMetadata(
                1,
                packId,
                state,
                DateTimeOffset.UtcNow);
            await using (var stateStream = CreateFile(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stateStream,
                    metadata,
                    StateSerializerOptions,
                    cancellationToken);
            }

            File.Move(temporaryPath, statePath, true);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    private static FileStream CreateFile(string path) => new(
        path,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None,
        4096,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static FileStream OpenFile(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        4096,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static async Task<LocalStateReadResult> LoadStateFileAsync(
        string statePath,
        CancellationToken cancellationToken)
    {
        await using var stateStream = OpenFile(statePath);
        var metadata = await JsonSerializer.DeserializeAsync<LocalCoachPackMetadata>(
            stateStream,
            StateSerializerOptions,
            cancellationToken);
        if (metadata is null
            || metadata.SchemaVersion != 1
            || string.IsNullOrWhiteSpace(metadata.PackId)
            || !Enum.IsDefined(metadata.State))
        {
            throw new InvalidDataException("Local Coach Pack state is invalid.");
        }

        return new LocalStateReadResult(
            metadata.PackId,
            metadata.State);
    }

    private static void ValidateState(LocalCoachPackState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

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

    private sealed record LocalStateReadResult(
        string PackId,
        LocalCoachPackState State);
}
