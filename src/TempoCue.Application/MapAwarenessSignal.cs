using TempoCue.Coaching;

namespace TempoCue.Application;

/// <summary>Сигнал карты, готовый для представления.</summary>
/// <remarks>
/// Несёт сработавшее правило, а не вид сигнала: текст уведомления принадлежит
/// набору, и приложение только показывает его. ADR 0016.
/// </remarks>
public sealed record MapAwarenessSignal(
    MapVisionRule Rule,
    string ParticipantId,
    string ChampionName,
    DateTimeOffset OccurredAt,
    double Confidence,
    double? MapX,
    double? MapY);
