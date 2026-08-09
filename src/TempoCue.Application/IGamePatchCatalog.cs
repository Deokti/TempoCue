namespace TempoCue.Application;

/// <summary>Предоставляет игровые патчи для создания и проверки Coach Pack.</summary>
public interface IGamePatchCatalog
{
    Task<GamePatchCatalogLoadResult> LoadAsync(
        CancellationToken cancellationToken = default);
}
