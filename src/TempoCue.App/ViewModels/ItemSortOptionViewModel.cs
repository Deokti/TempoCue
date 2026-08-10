namespace TempoCue.App.ViewModels;

/// <summary>Один локализованный режим сортировки каталога предметов.</summary>
public sealed record ItemSortOptionViewModel(ItemSortMode Mode, string DisplayName)
{
    // WPF использует строковое представление выбранного объекта вне ItemTemplate.
    public override string ToString() => DisplayName;
}
