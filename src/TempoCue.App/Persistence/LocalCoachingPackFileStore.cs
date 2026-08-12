using System.IO;
using System.Text.Json;
using TempoCue.Application;

namespace TempoCue.App.Persistence;

/// <summary>
/// Общий атомарный файловый механизм для разных переносимых типов Coach Pack.
/// Доменный формат задаётся функциями чтения, записи и получения метаданных.
/// </summary>
internal sealed class LocalCoachingPackFileStore<TPack>
    where TPack : class
{
    private readonly string _rootDirectory;
    private readonly string _entityName;
    private readonly string _fallbackFileStem;
    private readonly Func<TPack, string> _getId;
    private readonly Func<TPack, string> _getName;
    private readonly Action<TPack> _validate;
    private readonly Func<Stream, CancellationToken, Task<TPack>> _readAsync;
    private readonly Func<Stream, TPack, CancellationToken, Task> _writeAsync;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    internal LocalCoachingPackFileStore(
        string rootDirectory,
        string entityName,
        string fallbackFileStem,
        Func<TPack, string> getId,
        Func<TPack, string> getName,
        Action<TPack> validate,
        Func<Stream, CancellationToken, Task<TPack>> readAsync,
        Func<Stream, TPack, CancellationToken, Task> writeAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackFileStem);
        _rootDirectory = Path.GetFullPath(rootDirectory);
        _entityName = entityName;
        _fallbackFileStem = fallbackFileStem;
        _getId = getId ?? throw new ArgumentNullException(nameof(getId));
        _getName = getName ?? throw new ArgumentNullException(nameof(getName));
        _validate = validate ?? throw new ArgumentNullException(nameof(validate));
        _readAsync = readAsync ?? throw new ArgumentNullException(nameof(readAsync));
        _writeAsync = writeAsync ?? throw new ArgumentNullException(nameof(writeAsync));
    }

    internal async Task<StoredPackLibrarySnapshot<TPack>> LoadAsync(
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

    internal async Task<StoredPackSaveResult> SaveAsync(
        TPack pack,
        LocalCoachPackState state,
        LocalCoachPackOrigin origin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ValidateState(state);
        ValidateOrigin(origin);
        _validate(pack);

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            return await SaveCoreAsync(pack, state, origin, cancellationToken);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    internal async Task SetStateAsync(
        string packId,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        ValidateState(state);

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_rootDirectory);
            var statePath = GetStatePath(packId);
            var temporaryPath = CreateTemporaryPath(statePath);
            var origin = await ReadExistingOriginAsync(statePath, cancellationToken);
            try
            {
                await using (var stateStream = CreateFile(temporaryPath))
                {
                    await LocalCoachingPackStateSerializer.WriteAsync(
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
        finally
        {
            _operationLock.Release();
        }
    }

    internal async Task<bool> DeleteAsync(
        string packId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(_rootDirectory))
            {
                return false;
            }

            var packPath = GetPackPath(packId);
            if (!File.Exists(packPath))
            {
                return false;
            }

            await using (var packStream = OpenFile(packPath))
            {
                var storedPack = await _readAsync(packStream, cancellationToken);
                if (!IdsEqual(_getId(storedPack), packId))
                {
                    throw new InvalidDataException(
                        $"Stored {_entityName} id does not match the requested id.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(packPath);
            var statePath = GetStatePath(packId);
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }

            return true;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<StoredPackLibrarySnapshot<TPack>> LoadCoreAsync(
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return new StoredPackLibrarySnapshot<TPack>(
                [],
                0,
                new Dictionary<string, LocalCoachPackState>(StringComparer.OrdinalIgnoreCase));
        }

        var stateFiles = await ReadStateFilesAsync(cancellationToken);
        var packFiles = await ReadPackFilesAsync(
            stateFiles.States,
            stateFiles.Origins,
            cancellationToken);
        return new StoredPackLibrarySnapshot<TPack>(
            packFiles.Entries
                .OrderByDescending(static entry => entry.SavedAtUtc)
                .ThenBy(entry => _getName(entry.Pack), StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            stateFiles.InvalidFileCount + packFiles.InvalidFileCount,
            new Dictionary<string, LocalCoachPackState>(
                stateFiles.States,
                StringComparer.OrdinalIgnoreCase));
    }

    private async Task<StoredPackSaveResult> SaveCoreAsync(
        TPack pack,
        LocalCoachPackState state,
        LocalCoachPackOrigin origin,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootDirectory);
        var packId = _getId(pack);
        var packPath = GetPackPath(packId);
        var statePath = GetStatePath(packId);
        var packTemporaryPath = CreateTemporaryPath(packPath);
        var stateTemporaryPath = CreateTemporaryPath(statePath);
        try
        {
            await EnsureDestinationBelongsToPackAsync(
                packPath,
                statePath,
                packId,
                origin,
                cancellationToken);
            await using (var packStream = CreateFile(packTemporaryPath))
            {
                await _writeAsync(packStream, pack, cancellationToken);
            }

            await using (var stateStream = CreateFile(stateTemporaryPath))
            {
                await LocalCoachingPackStateSerializer.WriteAsync(
                    stateStream,
                    packId,
                    state,
                    origin,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }

            File.Move(packTemporaryPath, packPath, true);
            File.Move(stateTemporaryPath, statePath, true);
            return new StoredPackSaveResult(packId, state, packPath);
        }
        finally
        {
            DeleteTemporaryFile(packTemporaryPath);
            DeleteTemporaryFile(stateTemporaryPath);
        }
    }

    private async Task<StateFilesReadResult> ReadStateFilesAsync(
        CancellationToken cancellationToken)
    {
        var states = new Dictionary<string, LocalCoachPackState>(StringComparer.OrdinalIgnoreCase);
        var origins = new Dictionary<string, LocalCoachPackOrigin>(StringComparer.OrdinalIgnoreCase);
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
                var state = await LocalCoachingPackStateSerializer.ReadAsync(
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

    private async Task<PackFilesReadResult<TPack>> ReadPackFilesAsync(
        IReadOnlyDictionary<string, LocalCoachPackState> states,
        IReadOnlyDictionary<string, LocalCoachPackOrigin> origins,
        CancellationToken cancellationToken)
    {
        var entries = new List<StoredPackEntry<TPack>>();
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
                var pack = await _readAsync(packStream, cancellationToken);
                var packId = _getId(pack);
                entries.Add(new StoredPackEntry<TPack>(
                    pack,
                    states.GetValueOrDefault(packId, LocalCoachPackState.Draft),
                    File.GetLastWriteTimeUtc(packPath),
                    origins.GetValueOrDefault(packId, LocalCoachPackOrigin.CreatedByUser)));
            }
            catch (Exception exception) when (IsInvalidStoredFile(exception))
            {
                invalidFileCount++;
            }
        }

        return new PackFilesReadResult<TPack>(entries, invalidFileCount);
    }

    private async Task EnsureDestinationBelongsToPackAsync(
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
            var storedPack = await _readAsync(packStream, cancellationToken);
            if (!IdsEqual(_getId(storedPack), packId))
            {
                throw new InvalidDataException(
                    $"A different {_entityName} uses the same safe file name.");
            }

            matchingPackExists = true;
        }

        LocalCoachPackOrigin? storedOrigin = null;
        if (File.Exists(statePath))
        {
            StoredCoachingPackState storedState;
            try
            {
                await using var stateStream = OpenFile(statePath);
                storedState = await LocalCoachingPackStateSerializer.ReadAsync(
                    stateStream,
                    cancellationToken);
            }
            catch (Exception exception) when (
                matchingPackExists
                && intendedOrigin == LocalCoachPackOrigin.CreatedByUser
                && exception is InvalidDataException or JsonException)
            {
                return;
            }

            if (!IdsEqual(storedState.PackId, packId))
            {
                throw new InvalidDataException(
                    $"A different {_entityName} state uses the same safe file name.");
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
                $"A {_entityName} cannot replace a local installation with a different origin.");
        }
    }

    private async Task<LocalCoachPackOrigin> ReadExistingOriginAsync(
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
            var state = await LocalCoachingPackStateSerializer.ReadAsync(
                stream,
                cancellationToken);
            return state.Origin;
        }
        catch (Exception exception) when (IsInvalidStoredFile(exception))
        {
            return LocalCoachPackOrigin.CreatedByUser;
        }
    }

    private string GetPackPath(string packId) =>
        Path.Combine(_rootDirectory, $"{CreateSafeFileStem(packId)}.json");

    private string GetStatePath(string packId) =>
        Path.Combine(_rootDirectory, $"{CreateSafeFileStem(packId)}.state.json");

    private string CreateSafeFileStem(string packId)
    {
        var safeCharacters = packId
            .Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '-')
            .ToArray();
        var result = new string(safeCharacters).Trim('.', '-', '_');
        return result.Length == 0 ? _fallbackFileStem : result;
    }

    private static bool IdsEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void ValidateState(LocalCoachPackState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    private static void ValidateOrigin(LocalCoachPackOrigin origin)
    {
        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
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
}

internal sealed record StoredPackEntry<TPack>(
    TPack Pack,
    LocalCoachPackState State,
    DateTimeOffset SavedAtUtc,
    LocalCoachPackOrigin Origin);

internal sealed record StoredPackLibrarySnapshot<TPack>(
    IReadOnlyList<StoredPackEntry<TPack>> Entries,
    int InvalidFileCount,
    IReadOnlyDictionary<string, LocalCoachPackState> PackStates);

internal sealed record StoredPackSaveResult(
    string PackId,
    LocalCoachPackState State,
    string DisplayPath);

internal sealed record PackFilesReadResult<TPack>(
    List<StoredPackEntry<TPack>> Entries,
    int InvalidFileCount);
