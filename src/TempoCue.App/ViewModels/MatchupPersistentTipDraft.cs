namespace TempoCue.App.ViewModels;

/// <summary>Снимок постоянной подсказки в несохранённом редакторе матчапа.</summary>
public sealed record MatchupPersistentTipDraft(
    string Id,
    string Title,
    string Text,
    string? ApplicablePhaseId,
    string? CustomSource);
