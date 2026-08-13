namespace TempoCue.Application;

/// <summary>Возвращает платформенный регион локальной установки League Client.</summary>
public interface ILeagueClientRegionSource
{
    Task<string?> ReadRegionAsync(CancellationToken cancellationToken = default);
}
