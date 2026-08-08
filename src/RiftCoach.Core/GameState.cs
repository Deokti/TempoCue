namespace RiftCoach.Core;

/// <summary>
/// Снимок данных о матче, необходимых движку для выбора совета.
/// Сейчас эти значения задаёт симулятор, а позднее их будут заполнять Riot API
/// и модуль анализа миникарты.
/// </summary>
public sealed record GameState
{
    /// <summary>Чемпион пользователя.</summary>
    public required string PlayerChampion { get; init; }

    /// <summary>Чемпион противника на линии.</summary>
    public required string OpponentChampion { get; init; }

    /// <summary>Текущий уровень чемпиона пользователя.</summary>
    public required int PlayerLevel { get; init; }

    /// <summary>
    /// Уникальные идентификаторы купленных предметов.
    /// HashSet используется, потому что нам важно быстро проверять наличие предмета.
    /// </summary>
    public IReadOnlySet<int> PlayerItemIds { get; init; } = new HashSet<int>();
}
