namespace TempoCue.Core;

/// <summary>Проверенное состояние союзной команды во время выбора чемпионов.</summary>
public sealed record ChampionSelectState
{
    public ChampionSelectState(
        int localPlayerCellId,
        IReadOnlyList<ChampionSelectParticipant> participants)
    {
        if (localPlayerCellId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(localPlayerCellId));
        }

        ArgumentNullException.ThrowIfNull(participants);
        if (participants.Count == 0)
        {
            throw new ArgumentException(
                "Champion Select должен содержать хотя бы локального участника.",
                nameof(participants));
        }

        if (participants.Any(static participant => participant is null)
            || participants.Select(static participant => participant.CellId).Distinct().Count()
                != participants.Count)
        {
            throw new ArgumentException(
                "Слоты Champion Select должны быть непустыми и иметь уникальные cellId.",
                nameof(participants));
        }

        if (participants.Count(static participant => participant.IsLocalPlayer) != 1
            || participants.Single(static participant => participant.IsLocalPlayer).CellId
                != localPlayerCellId)
        {
            throw new ArgumentException(
                "Champion Select должен содержать ровно один корректный локальный слот.",
                nameof(participants));
        }

        LocalPlayerCellId = localPlayerCellId;
        Participants = participants.ToArray();
    }

    public int LocalPlayerCellId { get; }

    public IReadOnlyList<ChampionSelectParticipant> Participants { get; }
}
