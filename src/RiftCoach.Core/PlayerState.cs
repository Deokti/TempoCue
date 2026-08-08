namespace RiftCoach.Core;

/// <summary>
/// Снимок доступного состояния одного участника матча.
/// Одна модель используется одинаково для пользователя, союзников и противников.
/// </summary>
public sealed record PlayerState
{
    /// <summary>
    /// Внутренний идентификатор участника в рамках матча.
    /// Логика не должна полагаться только на имя чемпиона: одинаковые чемпионы
    /// могут встретиться по разные стороны в некоторых режимах.
    /// </summary>
    public required string ParticipantId { get; init; }

    /// <summary>Название выбранного чемпиона.</summary>
    public required string ChampionName { get; init; }

    /// <summary>Команда участника.</summary>
    public required TeamSide Team { get; init; }

    /// <summary>Игровая позиция, если её удалось определить.</summary>
    public PlayerPosition Position { get; init; } = PlayerPosition.Unknown;

    /// <summary>Текущий уровень чемпиона.</summary>
    public required int Level { get; init; }

    /// <summary>
    /// Уникальные идентификаторы купленных предметов.
    /// HashSet используется, потому что нам важно быстро проверять наличие предмета.
    /// </summary>
    public IReadOnlySet<int> ItemIds { get; init; } = new HashSet<int>();

    /// <summary>Показывает, находится ли чемпион в состоянии смерти.</summary>
    public bool IsDead { get; init; }

    /// <summary>Оставшееся время до возрождения в секундах.</summary>
    public double RespawnTimerSeconds { get; init; }

    /// <summary>Показывает, управляется ли участник ботом.</summary>
    public bool IsBot { get; init; }
}
