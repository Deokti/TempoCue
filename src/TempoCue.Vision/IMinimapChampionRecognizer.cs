namespace TempoCue.Vision;

/// <summary>Подтверждает предложения областей портретами участников активного матча.</summary>
internal interface IMinimapChampionRecognizer
{
    MinimapChampionRecognitionResult Recognize(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<MinimapChampionReference> references);
}
