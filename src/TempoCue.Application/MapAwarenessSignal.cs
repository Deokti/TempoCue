namespace TempoCue.Application;

/// <summary>Семантический сигнал карты, готовый для представления или дальнейшего планирования.</summary>
public sealed record MapAwarenessSignal(
    MapAwarenessSignalKind Kind,
    string ParticipantId,
    string ChampionName,
    DateTimeOffset OccurredAt,
    double Confidence,
    double? MapX,
    double? MapY);
