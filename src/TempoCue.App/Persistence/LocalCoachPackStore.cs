using System.IO;
using System.Text.Json;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Persistence;

/// <summary>Хранит пользовательские Coach Packs и их локальное состояние в LocalAppData.</summary>
public sealed class LocalCoachPackStore : ICoachPackStore
{
    private readonly string _rootDirectory;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public LocalCoachPackStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    public async Task<CoachPackLibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            return await LoadCoreAsync(cancellationToken);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<CoachPackLibrarySnapshot> LoadCoreAsync(
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return new CoachPackLibrarySnapshot(
                [],
                0,
                new Dictionary<string, LocalCoachPackState>(StringComparer.OrdinalIgnoreCase));
        }

        var stateFiles = await ReadStateFilesAsync(cancellationToken);
        var packFiles = await ReadPackFilesAsync(
            stateFiles.States,
            stateFiles.Origins,
            cancellationToken);

        return new CoachPackLibrarySnapshot(
            packFiles.Entries
                .OrderByDescending(static entry => entry.SavedAtUtc)
                .ThenBy(static entry => entry.Pack.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            stateFiles.InvalidFileCount + packFiles.InvalidFileCount,
            new Dictionary<string, LocalCoachPackState>(
                stateFiles.States,
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

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            return await SaveCoreAsync(
                pack,
                state,
                LocalCoachPackOrigin.CreatedByUser,
                cancellationToken);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<CoachPackSaveResult> ImportAsync(
        CoachPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ValidateState(state);
        CoachPackValidator.Validate(pack);

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            return await SaveCoreAsync(
                pack,
                state,
                LocalCoachPackOrigin.Imported,
                cancellationToken);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<CoachPackSaveResult> SaveCoreAsync(
        CoachPack pack,
        LocalCoachPackState state,
        LocalCoachPackOrigin origin,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootDirectory);
        var fileStem = CreateSafeFileStem(pack.Id);
        var packPath = Path.Combine(_rootDirectory, $"{fileStem}.json");
        var statePath = Path.Combine(_rootDirectory, $"{fileStem}.state.json");
        var packTemporaryPath = CreateTemporaryPath(packPath);
        var stateTemporaryPath = CreateTemporaryPath(statePath);

        try
        {
            await EnsureDestinationBelongsToPackAsync(
                packPath,
                statePath,
                pack.Id,
                origin,
                cancellationToken);
            await using (var packStream = CreateFile(packTemporaryPath))
            {
                await CoachPackWriter.WriteAsync(packStream, pack, cancellationToken);
            }

            await using (var stateStream = CreateFile(stateTemporaryPath))
            {
                await LocalCoachPackStateSerializer.WriteAsync(
                    stateStream,
                    pack.Id,
                    state,
                    origin,
                    DateTimeOffset.UtcNow,
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

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await SetStateCoreAsync(packId, state, cancellationToken);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task SetStateCoreAsync(
        string packId,
        LocalCoachPackState state,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootDirectory);
        var fileStem = CreateSafeFileStem(packId);
        var statePath = Path.Combine(_rootDirectory, $"{fileStem}.state.json");
        var temporaryPath = CreateTemporaryPath(statePath);
        var origin = await ReadExistingOriginAsync(statePath, cancellationToken);

        try
        {
            await using (var stateStream = CreateFile(temporaryPath))
            {
                await LocalCoachPackStateSerializer.WriteAsync(
                    stateStream,
                    packId,
                    state,
                    origin,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }

            File.Move(temporaryPath, statePath, true);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    public async Task<bool> DeleteAsync(
        string packId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            return await DeleteCoreAsync(packId, cancellationToken);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<bool> DeleteCoreAsync(
        string packId,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return false;
        }

        var fileStem = CreateSafeFileStem(packId);
        var packPath = Path.Combine(_rootDirectory, $"{fileStem}.json");
        if (!File.Exists(packPath))
        {
            return false;
        }

        // Перед необратимым удалением убеждаемся, что безопасное имя файла не привело к коллизии ID.
        await using (var packStream = OpenFile(packPath))
        {
            var storedPack = await CoachPackLoader.LoadAsync(packStream, cancellationToken);
            if (!string.Equals(storedPack.Id, packId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Stored Coach Pack id does not match the requested id.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(packPath);
        var statePath = Path.Combine(_rootDirectory, $"{fileStem}.state.json");
        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }

        return true;
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

    private async Task<StateFilesReadResult> ReadStateFilesAsync(
        CancellationToken cancellationToken)
    {
        var states = new Dictionary<string, LocalCoachPackState>(
            StringComparer.OrdinalIgnoreCase);
        var origins = new Dictionary<string, LocalCoachPackOrigin>(
            StringComparer.OrdinalIgnoreCase);
        var invalidFileCount = 0;
        foreach (var statePath in Directory.EnumerateFiles(
                     _rootDirectory,
                     "*.state.json",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stateStream = OpenFile(statePath);
                var state = await LocalCoachPackStateSerializer.ReadAsync(
                    stateStream,
                    cancellationToken);
                states[state.PackId] = state.State;
                origins[state.PackId] = state.Origin;
            }
            catch (Exception exception) when (IsInvalidStoredFile(exception))
            {
                invalidFileCount++;
            }
        }

        return new StateFilesReadResult(states, origins, invalidFileCount);
    }

    private async Task<PackFilesReadResult> ReadPackFilesAsync(
        IReadOnlyDictionary<string, LocalCoachPackState> states,
        IReadOnlyDictionary<string, LocalCoachPackOrigin> origins,
        CancellationToken cancellationToken)
    {
        var entries = new List<LocalCoachPackEntry>();
        var invalidFileCount = 0;
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
                entries.Add(new LocalCoachPackEntry(
                    pack,
                    states.GetValueOrDefault(pack.Id, LocalCoachPackState.Draft),
                    File.GetLastWriteTimeUtc(packPath),
                    origins.GetValueOrDefault(
                        pack.Id,
                        LocalCoachPackOrigin.CreatedByUser)));
            }
            catch (Exception exception) when (IsInvalidStoredFile(exception))
            {
                invalidFileCount++;
            }
        }

        return new PackFilesReadResult(entries, invalidFileCount);
    }

    private static void ValidateState(LocalCoachPackState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    private static async Task<LocalCoachPackOrigin> ReadExistingOriginAsync(
        string statePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(statePath))
        {
            return LocalCoachPackOrigin.CreatedByUser;
        }

        try
        {
            await using var stream = OpenFile(statePath);
            var state = await LocalCoachPackStateSerializer.ReadAsync(stream, cancellationToken);
            return state.Origin;
        }
        catch (Exception exception) when (IsInvalidStoredFile(exception))
        {
            return LocalCoachPackOrigin.CreatedByUser;
        }
    }

    private static async Task EnsureDestinationBelongsToPackAsync(
        string packPath,
        string statePath,
        string packId,
        LocalCoachPackOrigin intendedOrigin,
        CancellationToken cancellationToken)
    {
        var matchingPackExists = false;
        if (File.Exists(packPath))
        {
            await using var packStream = OpenFile(packPath);
            var storedPack = await CoachPackLoader.LoadAsync(packStream, cancellationToken);
            if (!string.Equals(storedPack.Id, packId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "A different Coach Pack uses the same safe file name.");
            }

            matchingPackExists = true;
        }

        LocalCoachPackOrigin? storedOrigin = null;
        if (File.Exists(statePath))
        {
            StoredCoachPackState storedState;
            try
            {
                await using var stateStream = OpenFile(statePath);
                storedState = await LocalCoachPackStateSerializer.ReadAsync(
                    stateStream,
                    cancellationToken);
            }
            catch (Exception exception) when (
                matchingPackExists
                && intendedOrigin == LocalCoachPackOrigin.CreatedByUser
                && exception is InvalidDataException or JsonException)
            {
                // Повреждённое состояние собственного набора восстанавливается при сохранении.
                return;
            }

            if (!string.Equals(storedState.PackId, packId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "A different Coach Pack state uses the same safe file name.");
            }

            storedOrigin = storedState.Origin;
        }

        if (!matchingPackExists)
        {
            return;
        }

        var replacesDifferentOrigin = intendedOrigin switch
        {
            LocalCoachPackOrigin.Imported => storedOrigin != LocalCoachPackOrigin.Imported,
            LocalCoachPackOrigin.CreatedByUser => storedOrigin == LocalCoachPackOrigin.Imported,
            _ => true
        };
        if (replacesDifferentOrigin)
        {
            throw new InvalidDataException(
                "A Coach Pack cannot replace a local installation with a different origin.");
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

    private static bool IsInvalidStoredFile(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException;

    private sealed record StateFilesReadResult(
        Dictionary<string, LocalCoachPackState> States,
        Dictionary<string, LocalCoachPackOrigin> Origins,
        int InvalidFileCount);

    private sealed record PackFilesReadResult(
        List<LocalCoachPackEntry> Entries,
        int InvalidFileCount);
}
