using System.Diagnostics;
using System.Net.Security;
using System.Text.Json;
using TempoCue.Application;

namespace TempoCue.Riot;

/// <summary>
/// Запрашивает официальный локальный Live Client Data API запущенной игры.
/// Обычный серверный Riot API key для этого локального endpoint не используется.
/// </summary>
public sealed class LiveClientDataClient : IMatchSnapshotSource, IDisposable
{
    public static readonly Uri AllGameDataEndpoint =
        new("https://127.0.0.1:2999/liveclientdata/allgamedata");

    public static readonly Uri ActivePlayerNameEndpoint =
        new("https://127.0.0.1:2999/liveclientdata/activeplayername");

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly Func<bool> _isGameProcessRunning;
    private readonly bool _checkGameProcessBeforeRequest;
    private string? _cachedActivePlayerName;

    /// <summary>Создаёт клиент с настройками для локального HTTPS endpoint League.</summary>
    public LiveClientDataClient()
        : this(
            CreateHttpClient(),
            ownsHttpClient: true,
            IsLeagueGameRunning,
            checkGameProcessBeforeRequest: true)
    {
    }

    /// <summary>
    /// Позволяет передать HttpClient извне. Этот конструктор используется тестами,
    /// чтобы проверить преобразование ответа без запущенной League of Legends.
    /// </summary>
    public LiveClientDataClient(HttpClient httpClient)
        : this(
            httpClient,
            ownsHttpClient: false,
            static () => false,
            checkGameProcessBeforeRequest: false)
    {
    }

    /// <summary>Позволяет тестам задать состояние игрового процесса.</summary>
    public LiveClientDataClient(HttpClient httpClient, Func<bool> isGameProcessRunning)
        : this(
            httpClient,
            ownsHttpClient: false,
            isGameProcessRunning,
            checkGameProcessBeforeRequest: true)
    {
    }

    private LiveClientDataClient(
        HttpClient httpClient,
        bool ownsHttpClient,
        Func<bool> isGameProcessRunning,
        bool checkGameProcessBeforeRequest)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(isGameProcessRunning);
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _isGameProcessRunning = isGameProcessRunning;
        _checkGameProcessBeforeRequest = checkGameProcessBeforeRequest;
    }

    /// <summary>Получает один полный снимок активного матча.</summary>
    public async Task<MatchSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .GetAsync(
                AllGameDataEndpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var data = await JsonSerializer
            .DeserializeAsync<LiveClientAllGameDataDto>(
                contentStream,
                ReadOptions,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("Riot API вернул пустой JSON активного матча.");

        var activePlayerName = await ResolveActivePlayerNameAsync(
            data,
            cancellationToken).ConfigureAwait(false);
        var match = LiveClientDataMapper.Map(data, activePlayerName);
        var events = LiveClientDataMapper.MapEvents(data);

        return new MatchSnapshot(match, events, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Преобразует особенности локального HTTP endpoint в типизированные исходы
    /// сценария активного матча.
    /// </summary>
    public async Task<MatchSnapshotReadResult> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Без игрового процесса loopback endpoint заведомо недоступен. Предварительная
            // проверка не создаёт HTTPS-запросы с таймаутом во время простоя приложения.
            if (_checkGameProcessBeforeRequest && !_isGameProcessRunning())
            {
                Volatile.Write(ref _cachedActivePlayerName, null);
                return MatchSnapshotReadResult.NoActiveMatch();
            }

            return MatchSnapshotReadResult.Success(
                await GetSnapshotAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return GetUnavailableResult();
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            return GetUnavailableResult();
        }
        catch (HttpRequestException exception) when (exception.StatusCode is
            System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.BadGateway
            or System.Net.HttpStatusCode.ServiceUnavailable
            or System.Net.HttpStatusCode.GatewayTimeout)
        {
            return GetUnavailableResult();
        }
        catch (HttpRequestException exception)
        {
            Trace.TraceError(
                "Live Client Data вернул ошибку HTTP {0}: {1}",
                exception.StatusCode,
                exception.Message);
            return MatchSnapshotReadResult.Failed();
        }
        catch (JsonException exception)
        {
            Trace.TraceError(
                "Не удалось разобрать JSON Live Client Data: {0}",
                exception.Message);
            return MatchSnapshotReadResult.InvalidData();
        }
        catch (InvalidDataException exception)
        {
            Trace.TraceError(
                "Live Client Data не содержит обязательные данные: {0}",
                exception.Message);
            return MatchSnapshotReadResult.InvalidData();
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "Непредвиденная ошибка Live Client Data ({0}): {1}",
                exception.GetType().Name,
                exception.Message);
            return MatchSnapshotReadResult.Failed();
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private MatchSnapshotReadResult GetUnavailableResult() =>
        _isGameProcessRunning()
            ? MatchSnapshotReadResult.GameLoading()
            : MatchSnapshotReadResult.NoActiveMatch();

    private async Task<string?> ResolveActivePlayerNameAsync(
        LiveClientAllGameDataDto data,
        CancellationToken cancellationToken)
    {
        if (LiveClientDataMapper.TryResolveActivePlayerId(data, null, out _))
        {
            return null;
        }

        var cachedName = Volatile.Read(ref _cachedActivePlayerName);
        if (LiveClientDataMapper.TryResolveActivePlayerId(data, cachedName, out _))
        {
            return cachedName;
        }

        using var response = await _httpClient
            .GetAsync(
                ActivePlayerNameEndpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var activePlayerName = await JsonSerializer.DeserializeAsync<string>(
            contentStream,
            ReadOptions,
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(activePlayerName))
        {
            throw new InvalidDataException(
                "Riot API не вернул имя активного игрока.");
        }

        Volatile.Write(ref _cachedActivePlayerName, activePlayerName);
        return activePlayerName;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            // Игра использует локальный самоподписанный сертификат. Исключение делается
            // только для loopback-адреса и порта Live Client Data API, а не для сети вообще.
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                errors == SslPolicyErrors.None
                || request.RequestUri is { IsLoopback: true, Port: 2999 }
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
    }

    private static bool IsLeagueGameRunning()
    {
        var processes = Process.GetProcessesByName("League of Legends");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
