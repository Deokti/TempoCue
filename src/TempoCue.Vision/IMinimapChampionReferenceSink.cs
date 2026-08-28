namespace TempoCue.Vision;

/// <summary>
/// Принимает небольшой набор портретов участников матча для подтверждения меток миникарты.
/// </summary>
public interface IMinimapChampionReferenceSink
{
    void SetChampionReferences(IReadOnlyList<MinimapChampionReference> references);
}
