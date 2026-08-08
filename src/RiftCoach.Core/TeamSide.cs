namespace RiftCoach.Core;

/// <summary>
/// Сторона участника на карте. Названия соответствуют значениям ORDER и CHAOS,
/// которые возвращает Riot Live Client Data API.
/// </summary>
public enum TeamSide
{
    /// <summary>Источник данных пока не сообщил сторону участника.</summary>
    Unknown,

    /// <summary>Синяя сторона карты.</summary>
    Order,

    /// <summary>Красная сторона карты.</summary>
    Chaos
}
