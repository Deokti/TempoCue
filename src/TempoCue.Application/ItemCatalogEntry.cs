namespace TempoCue.Application;

/// <summary>Стабильные данные предмета без зависимости от формата Riot Data Dragon.</summary>
public sealed record ItemCatalogEntry(
    int Id,
    string DisplayName,
    string Description,
    Uri ImageUri,
    int Price,
    IReadOnlyList<string> Tags);
