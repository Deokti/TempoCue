using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Исход чтения локальной стадии выбора чемпионов.</summary>
public enum ChampionSelectReadStatus
{
    Success,
    NotInChampionSelect,
    ClientUnavailable,
    InvalidData,
    Failed
}

/// <summary>Типизированный результат источника Champion Select без Riot DTO.</summary>
public sealed record ChampionSelectReadResult
{
    private ChampionSelectReadResult(
        ChampionSelectReadStatus status,
        ChampionSelectState? state = null)
    {
        Status = status;
        State = state;
    }

    public ChampionSelectReadStatus Status { get; }

    public ChampionSelectState? State { get; }

    public static ChampionSelectReadResult Success(ChampionSelectState state) =>
        new(
            ChampionSelectReadStatus.Success,
            state ?? throw new ArgumentNullException(nameof(state)));

    public static ChampionSelectReadResult NotInChampionSelect() =>
        new(ChampionSelectReadStatus.NotInChampionSelect);

    public static ChampionSelectReadResult ClientUnavailable() =>
        new(ChampionSelectReadStatus.ClientUnavailable);

    public static ChampionSelectReadResult InvalidData() =>
        new(ChampionSelectReadStatus.InvalidData);

    public static ChampionSelectReadResult Failed() =>
        new(ChampionSelectReadStatus.Failed);
}
