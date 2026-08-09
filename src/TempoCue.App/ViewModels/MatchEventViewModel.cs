namespace TempoCue.App.ViewModels;

public enum MatchEventAccent
{
    Neutral,
    Ally,
    Enemy,
    Objective
}

/// <summary>Локализованная строка факта для вкладки событий.</summary>
public sealed record MatchEventViewModel(
    string Time,
    string Title,
    string Details,
    MatchEventAccent Accent);
