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

    private static readonly Uri PlayerScoresEndpoint =
        new("https://127.0.0.1:2999/liveclientdata/playerscores");

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

        var currentScores = await GetAllPlayerScoresAsync(data, cancellationToken)
            .ConfigureAwait(false);
        var match = LiveClientDataMapper.Map(data, currentScores);
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
        catch (HttpRequestException)
        {
            return MatchSnapshotReadResult.Failed();
        }
        catch (JsonException)
        {
            return MatchSnapshotReadResult.InvalidData();
        }
        catch (InvalidDataException)
        {
            return MatchSnapshotReadResult.InvalidData();
        }
        catch (Exception)
        {
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

    private async Task<IReadOnlyDictionary<string, LiveClientScoresDto>> GetAllPlayerScoresAsync(
        LiveClientAllGameDataDto data,
        CancellationToken cancellationToken)
    {
        var requests = data.AllPlayers
            .Select(player => GetPlayerScoresAsync(player, cancellationToken))
            .ToArray();
        var results = await Task.WhenAll(requests).ConfigureAwait(false);

        return results
            .Where(result => result.HasValue)
            .Select(result => result.GetValueOrDefault())
            .GroupBy(result => result.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Value,
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<KeyValuePair<string, LiveClientScoresDto>?> GetPlayerScoresAsync(
        LiveClientPlayerDto player,
        CancellationToken cancellationToken)
    {
        var riotId = LiveClientDataMapper.GetParticipantId(
            player.RiotId,
            player.SummonerName);

        if (string.IsNullOrWhiteSpace(riotId))
        {
            return null;
        }

        var endpoint = new UriBuilder(PlayerScoresEndpoint)
        {
            Query = $"riotId={Uri.EscapeDataString(riotId)}"
        }.Uri;

        try
        {
            using var response = await _httpClient
                .GetAsync(endpoint, cancellationToken)
                .ConfigureAwait(false);

            // Подробный endpoint — только необязательное уточнение общего снимка.
            // Некоторые боты и режимы не позволяют запросить показатели по Riot ID.
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            var scores = JsonSerializer.Deserialize<LiveClientScoresDto>(json, ReadOptions);

            return scores is null
                ? null
                : KeyValuePair.Create(riotId, scores);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
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
