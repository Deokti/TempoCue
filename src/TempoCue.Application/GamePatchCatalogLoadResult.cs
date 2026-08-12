namespace TempoCue.Application;

/// <summary>Результат получения списка патчей с явным признаком сохранённых данных.</summary>
public sealed record GamePatchCatalogLoadResult(
    IReadOnlyList<GamePatchCatalogEntry> Patches,
    bool IsFromCache = false)
{
    public bool IsAvailable => Patches.Count > 0;

    public static GamePatchCatalogLoadResult Unavailable() => new([]);
}
