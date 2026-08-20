using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.Riot;

/// <summary>
/// Читает видимое состояние Champion Select из локального League Client API.
/// Неподдерживаемый Riot контракт изолирован в DTO этого модуля.
/// </summary>
public sealed class LeagueClientChampionSelectClient : IChampionSelectSource, IDisposable
{
    private const string SessionPath = "lol-champ-select/v1/session";
    private const string VisibleIdentity = "VISIBLE";

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly Func<CancellationToken, Task<LeagueClientConnection?>> _connectionSource;
    private readonly Dictionary<long, RiotId> _visibleIdentityCache = [];

    public LeagueClientChampionSelectClient()
        : this(
            CreateHttpClient(),
            ownsHttpClient: true,
            new LeagueClientConnectionSource().ReadAsync)
    {
    }

    /// <summary>Позволяет тестировать локальный контракт без запущенного клиента.</summary>
    public LeagueClientChampionSelectClient(
        HttpClient httpClient,
        Func<CancellationToken, Task<LeagueClientConnection?>> connectionSource)
        : this(httpClient, ownsHttpClient: false, connectionSource)
    {
    }

    private LeagueClientChampionSelectClient(
        HttpClient httpClient,
        bool ownsHttpClient,
        Func<CancellationToken, Task<LeagueClientConnection?>> connectionSource)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(connectionSource);
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _connectionSource = connectionSource;
    }

    public async Task<ChampionSelectReadResult> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connectionSource(cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                ClearSessionCache();
                return ChampionSelectReadResult.ClientUnavailable();
            }

            using var response = await SendAsync(
                connection,
                HttpMethod.Get,
                SessionPath,
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                ClearSessionCache();
                return ChampionSelectReadResult.NotInChampionSelect();
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var session = await JsonSerializer.DeserializeAsync<LeagueClientChampionSelectSessionDto>(
                stream,
                ReadOptions,
                cancellationToken).ConfigureAwait(false);
            if (session?.MyTeam is null
                || session.MyTeam.Count == 0
                || session.LocalPlayerCellId < 0)
            {
                ClearSessionCache();
                return ChampionSelectReadResult.InvalidData();
            }

            var participants = new List<ChampionSelectParticipant>(session.MyTeam.Count);
            foreach (var participant in session.MyTeam.OrderBy(static item => item.CellId))
            {
                participants.Add(await MapParticipantAsync(
                    participant,
                    session.LocalPlayerCellId,
                    connection,
                    cancellationToken).ConfigureAwait(false));
            }

            return ChampionSelectReadResult.Success(
                new ChampionSelectState(session.LocalPlayerCellId, participants));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            ClearSessionCache();
            return ChampionSelectReadResult.ClientUnavailable();
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            ClearSessionCache();
            return ChampionSelectReadResult.ClientUnavailable();
        }
        catch (JsonException exception)
        {
            Trace.TraceError(
                "Не удалось разобрать Champion Select: {0}",
                exception.Message);
            ClearSessionCache();
            return ChampionSelectReadResult.InvalidData();
        }
        catch (InvalidDataException exception)
        {
            Trace.TraceError("Некорректные данные Champion Select: {0}", exception.Message);
            ClearSessionCache();
            return ChampionSelectReadResult.InvalidData();
        }
        catch (HttpRequestException exception)
        {
            Trace.TraceError(
                "League Client API вернул ошибку HTTP {0} в Champion Select.",
                exception.StatusCode);
            ClearSessionCache();
            return ChampionSelectReadResult.Failed();
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "Непредвиденная ошибка Champion Select ({0}): {1}",
                exception.GetType().Name,
                exception.Message);
            ClearSessionCache();
            return ChampionSelectReadResult.Failed();
        }
    }

    public void Dispose()
    {
        ClearSessionCache();
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<ChampionSelectParticipant> MapParticipantAsync(
        LeagueClientChampionSelectParticipantDto participant,
        int localPlayerCellId,
        LeagueClientConnection connection,
        CancellationToken cancellationToken)
    {
        if (participant.CellId < 0 || participant.ChampionId < 0)
        {
            throw new InvalidDataException("Слот Champion Select содержит неверный идентификатор.");
        }

        var position = MapPosition(participant.AssignedPosition);
        var isLocalPlayer = participant.CellId == localPlayerCellId;
        if (string.Equals(
                participant.NameVisibilityType,
                "HIDDEN",
                StringComparison.OrdinalIgnoreCase))
        {
            return ChampionSelectParticipant.Anonymous(
                participant.CellId,
                participant.ChampionId,
                position,
                isLocalPlayer);
        }

        // Неизвестное или отсутствующее значение не трактуется как разрешение на раскрытие.
        if (!string.Equals(
                participant.NameVisibilityType,
                VisibleIdentity,
                StringComparison.OrdinalIgnoreCase)
            || participant.SummonerId <= 0)
        {
            return ChampionSelectParticipant.Unavailable(
                participant.CellId,
                participant.ChampionId,
                position,
                isLocalPlayer);
        }

        var riotId = await GetVisibleRiotIdAsync(
            participant.SummonerId,
            connection,
            cancellationToken).ConfigureAwait(false);
        return riotId is null
            ? ChampionSelectParticipant.Unavailable(
                participant.CellId,
                participant.ChampionId,
                position,
                isLocalPlayer)
            : ChampionSelectParticipant.Visible(
                participant.CellId,
                participant.ChampionId,
                position,
                isLocalPlayer,
                riotId);
    }

    private async Task<RiotId?> GetVisibleRiotIdAsync(
        long summonerId,
        LeagueClientConnection connection,
        CancellationToken cancellationToken)
    {
        if (_visibleIdentityCache.TryGetValue(summonerId, out var cached))
        {
            return cached;
        }

        using var response = await SendAsync(
            connection,
            HttpMethod.Get,
            $"lol-summoner/v1/summoners/{summonerId}",
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var summoner = await JsonSerializer.DeserializeAsync<LeagueClientSummonerDto>(
            stream,
            ReadOptions,
            cancellationToken).ConfigureAwait(false);
        if (!RiotId.TryCreate(summoner?.GameName, summoner?.TagLine, out var riotId))
        {
            return null;
        }

        _visibleIdentityCache[summonerId] = riotId;
        return riotId;
    }

    private async Task<HttpResponseMessage> SendAsync(
        LeagueClientConnection connection,
        HttpMethod method,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            method,
            new Uri(connection.BaseAddress, relativePath));
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"riot:{connection.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
    }

    private static PlayerPosition MapPosition(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "TOP" => PlayerPosition.Top,
            "JUNGLE" => PlayerPosition.Jungle,
            "MIDDLE" or "MID" => PlayerPosition.Middle,
            "BOTTOM" or "BOT" => PlayerPosition.Bottom,
            "UTILITY" or "SUPPORT" => PlayerPosition.Support,
            _ => PlayerPosition.Unknown
        };

    private void ClearSessionCache() => _visibleIdentityCache.Clear();

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                errors == SslPolicyErrors.None
                || request.RequestUri is { IsLoopback: true }
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
    }
}
