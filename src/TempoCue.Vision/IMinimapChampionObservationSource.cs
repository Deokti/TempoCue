namespace TempoCue.Vision;

/// <summary>Публикует распознавания отдельных кадров без принятия решения о видимости.</summary>
public interface IMinimapChampionObservationSource
{
    event EventHandler<MinimapChampionObservationEventArgs> ObservationAvailable;
}
