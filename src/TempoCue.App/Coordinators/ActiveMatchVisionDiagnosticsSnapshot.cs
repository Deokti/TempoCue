using TempoCue.Core;
using TempoCue.Application;
using TempoCue.Vision;

namespace TempoCue.App.Coordinators;

/// <summary>
/// Хранит проверяемую связь между участниками Live Client Data и относительными командами Vision.
/// </summary>
internal sealed record ActiveMatchVisionDiagnosticsSnapshot(
    TeamSide ActivePlayerTeam,
    IReadOnlyList<ActiveMatchVisionParticipantSnapshot> Participants)
{
    public static ActiveMatchVisionDiagnosticsSnapshot Empty { get; } =
        new(TeamSide.Unknown, []);
}

/// <summary>Описывает одного участника без переноса Riot-моделей в модуль Vision.</summary>
internal sealed record ActiveMatchVisionParticipantSnapshot(
    string ParticipantId,
    string ChampionName,
    string? ChampionId,
    PlayerPosition Position,
    TeamSide RawTeam,
    MinimapMarkerAffiliation RelativeAffiliation,
    ChampionPortraitReferenceStatus ReferenceStatus =
        ChampionPortraitReferenceStatus.Ready,
    bool IsDead = false,
    ParticipantVisibilityStatus VisibilityStatus =
        ParticipantVisibilityStatus.Unknown,
    DateTimeOffset? LastSeenAt = null,
    /// <summary>
    /// Игровое время последнего наблюдения. Часы для этого не годятся: матч
    /// можно поставить на паузу, и разница «сейчас минус тогда» перестанет
    /// совпадать с тем, что видит игрок на своём таймере.
    /// </summary>
    TimeSpan? LastSeenGameTime = null,
    double VisibilityConfidence = 0,
    double? MapX = null,
    double? MapY = null);

/// <summary>Отделяет незавершённую загрузку эталона от её конечной ошибки.</summary>
internal enum ChampionPortraitReferenceStatus
{
    Pending,
    Ready,
    Unavailable
}

/// <summary>Предоставляет UI последний неизменяемый снимок подготовки Vision.</summary>
internal interface IActiveMatchVisionDiagnosticsSource
{
    ActiveMatchVisionDiagnosticsSnapshot DiagnosticsSnapshot { get; }
}
