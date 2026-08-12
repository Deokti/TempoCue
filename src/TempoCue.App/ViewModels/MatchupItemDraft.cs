namespace TempoCue.App.ViewModels;

/// <summary>
/// Ссылка на предмет в черновике. Числовой ID станет частью Coach Pack,
/// а отображаемые данные нужны только редактору до сохранения.
/// </summary>
public sealed record MatchupItemDraft(
    int Id,
    string DisplayName,
    Uri ImageUri);
