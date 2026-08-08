using System.Net.Security;
using System.Text.Json;

namespace TempoCue.Riot;

/// <summary>
/// Запрашивает официальный локальный Live Client Data API запущенной игры.
/// Обычный серверный Riot API key для этого локального endpoint не используется.
/// </summary>
public sealed class LiveClientDataClient : IDisposable
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

    /// <summary>Создаёт клиент с настройками для локального HTTPS endpoint League.</summary>
    public LiveClientDataClient()
        : this(CreateHttpClient(), ownsHttpClient: true)
    {
    }

    /// <summary>
    /// Позволяет передать HttpClient извне. Этот конструктор используется тестами,
    /// чтобы проверить преобразование ответа без запущенной League of Legends.
    /// </summary>
    public LiveClientDataClient(HttpClient httpClient)
        : this(httpClient, ownsHttpClient: false)
    {
    }

    private LiveClientDataClient(HttpClient httpClient, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    /// <summary>Получает один полный снимок активного матча.</summary>
    public async Task<LiveClientSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
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

        // Повторная сериализация нужна только для удобного отображения JSON в приложении.
        using var document = JsonDocument.Parse(rawJson);
        var displayJson = JsonSerializer.Serialize(document.RootElement, DisplayOptions);

        return new LiveClientSnapshot(match, displayJson, DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
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
}
