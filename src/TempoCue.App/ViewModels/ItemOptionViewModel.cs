namespace TempoCue.App.ViewModels;

/// <summary>Один предмет в переиспользуемом каталоге выбора.</summary>
public sealed record ItemOptionViewModel(
    int Id,
    string DisplayName,
    string Description,
    Uri ImageUri,
    int Price,
    IReadOnlyList<string> Tags);
