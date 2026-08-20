using System.Diagnostics;
using System.Text.Json;
using TempoCue.Application;

namespace TempoCue.Riot;

/// <summary>
/// Читает регион из локальной конфигурации League Client без API-ключа и сетевого запроса.
/// </summary>
public sealed class LeagueClientRegionSource : ILeagueClientRegionSource
{
    private const string SettingsRelativePath = "Config\\LeagueClientSettings.yaml";

    private readonly string _installationsPath;
    private readonly Func<IReadOnlyList<string>> _fallbackInstallDirectories;

    public LeagueClientRegionSource()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Riot Games",
                "RiotClientInstalls.json"),
            FindRunningClientDirectories)
    {
    }

    /// <summary>Позволяет проверить чтение конфигурации без установленной League.</summary>
    public LeagueClientRegionSource(string installationsPath)
        : this(installationsPath, static () => [])
    {
    }

    private LeagueClientRegionSource(
        string installationsPath,
        Func<IReadOnlyList<string>> fallbackInstallDirectories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationsPath);
        ArgumentNullException.ThrowIfNull(fallbackInstallDirectories);
        _installationsPath = installationsPath;
        _fallbackInstallDirectories = fallbackInstallDirectories;
    }

    public async Task<string?> ReadRegionAsync(CancellationToken cancellationToken = default)
    {
        var installDirectories = await ReadManifestDirectoriesAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var directory in installDirectories.Concat(_fallbackInstallDirectories()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var settingsPath = Path.Combine(directory, SettingsRelativePath);
            var region = await ReadRegionFromSettingsAsync(settingsPath, cancellationToken)
                .ConfigureAwait(false);
            if (region is not null)
            {
                return region;
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
                bufferSize: 4096,
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
                .Select(static entry => NormalizeDirectory(entry.Name))
                .Where(static path => path is not null)
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
                "Не удалось прочитать список установок Riot Client: {0}",
                exception.GetType().Name);
            return [];
        }
    }

    private static async Task<string?> ReadRegionFromSettingsAsync(
        string settingsPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(
                settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true);
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                const string key = "region:";
                var trimmedLine = line.Trim();
                if (!trimmedLine.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = trimmedLine[key.Length..]
                    .Trim()
                    .Trim('"', '\'');
                return IsValidRegion(value) ? value.ToUpperInvariant() : null;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                "Не удалось прочитать регион League Client: {0}",
                exception.GetType().Name);
        }

        return null;
    }

    private static string? NormalizeDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(value.Replace('/', Path.DirectorySeparatorChar));
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsValidRegion(string value) =>
        value.Length is >= 2 and <= 8
        && value.All(char.IsLetterOrDigit);

    private static IReadOnlyList<string> FindRunningClientDirectories()
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddProcessDirectories("LeagueClientUx", directories);
        AddProcessDirectories("LeagueClient", directories);
        AddGameProcessDirectories(directories);
        return directories.ToArray();
    }

    private static void AddProcessDirectories(string processName, ISet<string> directories)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    var executableDirectory = Path.GetDirectoryName(process.MainModule?.FileName);
                    if (!string.IsNullOrWhiteSpace(executableDirectory))
                    {
                        directories.Add(executableDirectory);
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException
                    or System.ComponentModel.Win32Exception
                    or NotSupportedException)
                {
                    // Другой процесс может завершиться во время перечисления. Следующий
                    // кандидат или установочный манифест останутся безопасным запасным путём.
                }
            }
        }
    }

    private static void AddGameProcessDirectories(ISet<string> directories)
    {
        foreach (var process in Process.GetProcessesByName("League of Legends"))
        {
            using (process)
            {
                try
                {
                    var gameDirectory = Path.GetDirectoryName(process.MainModule?.FileName);
                    var installDirectory = gameDirectory is null
                        ? null
                        : Directory.GetParent(gameDirectory)?.FullName;
                    if (!string.IsNullOrWhiteSpace(installDirectory))
                    {
                        directories.Add(installDirectory);
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException
                    or System.ComponentModel.Win32Exception
                    or NotSupportedException)
                {
                    // Процесс игры мог завершиться между поиском и чтением его пути.
                }
            }
        }
    }
}
