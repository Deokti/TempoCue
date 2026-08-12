namespace TempoCue.App.ViewModels;

/// <summary>Передаёт выбранную карточку между независимыми экранами коучинга.</summary>
public sealed class CoachingPackEventArgs(CoachingPackCardViewModel card) : EventArgs
{
    public CoachingPackCardViewModel Card { get; } =
        card ?? throw new ArgumentNullException(nameof(card));
}
