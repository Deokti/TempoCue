namespace TempoCue.Application;

/// <summary>Предоставляет локализованный каталог предметов для выбранного игрового патча.</summary>
public interface IItemCatalog
{
    Task<ItemCatalogLoadResult> LoadAsync(
        string gamePatch,
        string languageCode,
        CancellationToken cancellationToken = default);
}
