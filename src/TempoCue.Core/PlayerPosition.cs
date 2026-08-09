namespace TempoCue.Core;

/// <summary>
/// Предполагаемая игровая позиция участника.
/// Unknown нужна для custom game и ситуаций, когда источник ещё не определил роль.
/// </summary>
public enum PlayerPosition
{
    Unknown,
    Top,
    Jungle,
    Middle,
    Bottom,
    Support
}
