namespace TempoCue.Application;

/// <summary>Результат поиска Coach Pack для текущего противостояния.</summary>
public enum ActiveCoachPackSelectionState
{
    MatchupUnavailable,
    NotFound,
    Selected,
    Conflict,
    LibraryUnavailable
}
