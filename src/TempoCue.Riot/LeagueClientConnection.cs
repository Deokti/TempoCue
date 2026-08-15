namespace TempoCue.Riot;

/// <summary>Краткоживущие реквизиты локального League Client из lockfile.</summary>
public sealed record LeagueClientConnection
{
    public LeagueClientConnection(Uri baseAddress, string password)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (!baseAddress.IsLoopback
            || !string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || baseAddress.Port <= 0)
        {
            throw new ArgumentException(
                "League Client должен использовать локальный HTTPS-адрес.",
                nameof(baseAddress));
        }

        BaseAddress = baseAddress;
        Password = password;
    }

    public Uri BaseAddress { get; }

    public string Password { get; }
}
