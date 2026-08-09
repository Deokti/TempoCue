namespace TempoCue.App.ViewModels;

/// <summary>Одна строка состава команды.</summary>
public sealed record ParticipantViewModel(
    string RoleCode,
    string ChampionInitials,
    string ChampionName,
    string Details,
    string Kda,
    int Level,
    bool IsActivePlayer,
    bool IsDead);
