namespace TempoCue.App.ViewModels;

/// <summary>Доступность типов нового Coach Pack в текущей итерации.</summary>
public sealed class PackTypeSelectionViewModel
{
    public bool IsMatchupAvailable => true;

    public bool IsGameControlAvailable => false;
}
