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

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions DisplayOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly Func<bool> _isGameProcessRunning;

    /// <summary>Создаёт клиент с настройками для локального HTTPS endpoint League.</summary>
    public LiveClientDataClient()
        : this(CreateHttpClient(), ownsHttpClient: true, IsLeagueGameRunning)
    {
    }

    /// <summary>
    /// Позволяет передать HttpClient извне. Этот конструктор используется тестами,
    /// чтобы проверить преобразование ответа без запущенной League of Legends.
    /// </summary>
    public LiveClientDataClient(HttpClient httpClient)
        : this(httpClient, ownsHttpClient: false, static () => false)
    {
    }

    /// <summary>Позволяет тестам задать состояние игрового процесса.</summary>
    public LiveClientDataClient(HttpClient httpClient, Func<bool> isGameProcessRunning)
        : this(httpClient, ownsHttpClient: false, isGameProcessRunning)
    {
    }

    private LiveClientDataClient(
        HttpClient httpClient,
        bool ownsHttpClient,
        Func<bool> isGameProcessRunning)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(isGameProcessRunning);
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _isGameProcessRunning = isGameProcessRunning;
    }

    /// <summary>Получает один полный снимок активного матча.</summary>
    public async Task<MatchSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .GetAsync(AllGameDataEndpoint, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var rawJson = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        var data = JsonSerializer.Deserialize<LiveClientAllGameDataDto>(rawJson, ReadOptions)
            ?? throw new InvalidDataException("Riot API вернул пустой JSON активного матча.");

        var match = LiveClientDataMapper.Map(data);
        var events = LiveClientDataMapper.MapEvents(data);

        // Повторная сериализация нужна только для удобного отображения JSON в приложении.
        using var document = JsonDocument.Parse(rawJson);
        var displayJson = JsonSerializer.Serialize(document.RootElement, DisplayOptions);

        return new MatchSnapshot(match, events, displayJson, DateTimeOffset.UtcNow);
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
