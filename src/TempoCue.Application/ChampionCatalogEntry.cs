namespace TempoCue.Application;

/// <summary>Стабильные данные чемпиона, доступные сценарию выбора без знания формата Riot.</summary>
public sealed record ChampionCatalogEntry(
    string Id,
    int NumericId,
    string DisplayName,
    Uri ImageUri);
