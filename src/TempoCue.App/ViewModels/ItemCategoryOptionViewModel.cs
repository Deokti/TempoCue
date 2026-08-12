namespace TempoCue.App.ViewModels;

/// <summary>Одна локализованная категория фильтра предметов.</summary>
public sealed class ItemCategoryOptionViewModel(
    ItemFilterCategory category,
    string displayName) : ObservableObject
{
    private bool _isSelected;

    public ItemFilterCategory Category { get; } = category;

    public string DisplayName { get; } = displayName;

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }
}
