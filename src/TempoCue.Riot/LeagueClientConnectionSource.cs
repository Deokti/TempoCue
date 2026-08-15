using System.Diagnostics;
using System.Text.Json;

namespace TempoCue.Riot;

/// <summary>Находит запущенный League Client и безопасно читает его lockfile.</summary>
public sealed class LeagueClientConnectionSource
{
    private readonly string _installationsPath;
    private readonly Func<IReadOnlyList<string>> _runningClientDirectories;

    public LeagueClientConnectionSource()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Riot Games",
                "RiotClientInstalls.json"),
            FindRunningClientDirectories)
    {
    }

    public LeagueClientConnectionSource(string installationsPath)
        : this(installationsPath, static () => [])
    {
    }

    private LeagueClientConnectionSource(
        string installationsPath,
        Func<IReadOnlyList<string>> runningClientDirectories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationsPath);
        ArgumentNullException.ThrowIfNull(runningClientDirectories);
        _installationsPath = installationsPath;
        _runningClientDirectories = runningClientDirectories;
    }

    public async Task<LeagueClientConnection?> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var manifestDirectories = await ReadManifestDirectoriesAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var directory in manifestDirectories
                     .Concat(_runningClientDirectories())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var connection = await ReadLockfileAsync(
                Path.Combine(directory, "lockfile"),
                cancellationToken).ConfigureAwait(false);
            if (connection is not null)
            {
                return connection;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<string>> ReadManifestDirectoriesAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_installationsPath))
        {
            return [];
        }

        try
        {
            await using var stream = new FileStream(
                _installationsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                4096,
                useAsync: true);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("associated_client", out var clients)
                || clients.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return clients.EnumerateObject()
                .Select(static entry => NormalizeInstallationDirectory(entry.Name))
                .Where(static directory => directory is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            Trace.TraceWarning(
                "Не удалось прочитать установки League Client: {0}",
                exception.GetType().Name);
            return [];
        }
    }

    private static async Task<LeagueClientConnection?> ReadLockfileAsync(
        string lockfilePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(lockfilePath))
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(
                lockfilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                4096,
                useAsync: true);
            using var reader = new StreamReader(stream);
            var value = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var parts = value.Split(':', 5, StringSplitOptions.TrimEntries);
            if (parts.Length != 5
                || !int.TryParse(parts[2], out var port)
                || port <= 0
                || string.IsNullOrWhiteSpace(parts[3])
                || !string.Equals(parts[4], Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new LeagueClientConnection(
                new Uri($"https://127.0.0.1:{port}/"),
                parts[3]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                "Не удалось прочитать League Client lockfile: {0}",
                exception.GetType().Name);
            return null;
        }
    }

    private static string? NormalizeInstallationDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var path = Path.GetFullPath(value.Replace('/', Path.DirectorySeparatorChar));
            return string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(path)
                : path;
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> FindRunningClientDirectories()
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var processName in new[] { "LeagueClientUx", "LeagueClient" })
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        var directory = Path.GetDirectoryName(process.MainModule?.FileName);
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            directories.Add(directory);
                        }
                    }
                    catch (Exception exception) when (exception is InvalidOperationException
                        or System.ComponentModel.Win32Exception
                        or NotSupportedException)
                    {
                        // Процесс мог завершиться; другой кандидат ещё может быть доступен.
                    }
                }
            }
        }

        return directories.ToArray();
    }
}
