using RiftCoach.Core;

namespace RiftCoach.Riot;

/// <summary>
/// Результат одного успешного запроса к активному матчу.
/// Содержит как исходный JSON для диагностики, так и нормализованную модель приложения.
/// </summary>
/// <param name="Match">Состояние матча, понятное остальным модулям RiftCoach.</param>
/// <param name="RawJson">Форматированный ответ Riot Live Client Data API.</param>
/// <param name="ReceivedAtUtc">Момент получения ответа в UTC.</param>
public sealed record LiveClientSnapshot(
    MatchState Match,
    string RawJson,
    DateTimeOffset ReceivedAtUtc);
