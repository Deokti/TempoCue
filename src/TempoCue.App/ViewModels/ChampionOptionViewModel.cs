namespace TempoCue.App.ViewModels;

/// <summary>Стабильное внутреннее имя чемпиона и данные для его отображения в редакторе.</summary>
public sealed record ChampionOptionViewModel(string Id, string DisplayName, Uri ImageUri)
{
    public string Initials => new(
        DisplayName
            .Where(char.IsLetterOrDigit)
            .Take(3)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
