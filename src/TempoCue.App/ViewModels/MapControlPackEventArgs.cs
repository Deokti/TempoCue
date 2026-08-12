namespace TempoCue.App.ViewModels;

/// <summary>Передаёт выбранную карточку контроля карты между View раздела коучинга.</summary>
public sealed class MapControlPackEventArgs : EventArgs
{
    public MapControlPackEventArgs(MapControlPackCardViewModel card)
    {
        Card = card ?? throw new ArgumentNullException(nameof(card));
    }

    public MapControlPackCardViewModel Card { get; }
}
