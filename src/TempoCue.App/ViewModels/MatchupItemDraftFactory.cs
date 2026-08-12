namespace TempoCue.App.ViewModels;

/// <summary>Преобразует данные каталога в компактные ссылки черновика Coach Pack.</summary>
internal static class MatchupItemDraftFactory
{
    public static IReadOnlyList<MatchupItemDraft> Create(
        IEnumerable<ItemOptionViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items
            .DistinctBy(static item => item.Id)
            .Select(static item => new MatchupItemDraft(
                item.Id,
                item.DisplayName,
                item.ImageUri))
            .ToArray();
    }
}
