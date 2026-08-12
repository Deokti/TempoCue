namespace TempoCue.Application;

/// <summary>Предоставляет локализованный каталог чемпионов для пользовательских сценариев.</summary>
public interface IChampionCatalog
{
    Task<ChampionCatalogLoadResult> LoadAsync(
        string languageCode,
        CancellationToken cancellationToken = default);
}
