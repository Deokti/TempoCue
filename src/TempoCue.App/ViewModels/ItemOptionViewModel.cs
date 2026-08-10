using TempoCue.Application;

namespace TempoCue.App.ViewModels;

/// <summary>Один предмет в переиспользуемом каталоге выбора.</summary>
public sealed class ItemOptionViewModel : ObservableObject
{
    private bool _isSelected;

    public ItemOptionViewModel(
        int id,
        string displayName,
        string description,
        Uri imageUri,
        int price,
        IReadOnlyList<string> tags,
        ItemCatalogCategory category,
        IReadOnlyList<ItemStatEntry> stats)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        ImageUri = imageUri;
        Price = price;
        Tags = tags;
        Category = category;
        Stats = stats;
    }

    public int Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public Uri ImageUri { get; }

    public int Price { get; }

    public IReadOnlyList<string> Tags { get; }

    public ItemCatalogCategory Category { get; }

    public IReadOnlyList<ItemStatEntry> Stats { get; }

    /// <summary>Предмет включён в текущий подтверждаемый набор модального окна.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }
}
