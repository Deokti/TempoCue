namespace TempoCue.App.ViewModels;

/// <summary>
/// Состояние загружаемого справочника, который может быть получен из сети или локального кэша.
/// </summary>
public enum CatalogViewState
{
    NotLoaded,
    Loading,
    Ready,
    Cached,
    Failed
}
