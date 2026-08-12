namespace TempoCue.App.ViewModels;

/// <summary>Снимок произвольного блока предметов в черновике матчапа.</summary>
public sealed record MatchupItemBlockDraft(
    string Id,
    string Name,
    string? WhenToChoose,
    IReadOnlyList<MatchupItemDraft> Items);
