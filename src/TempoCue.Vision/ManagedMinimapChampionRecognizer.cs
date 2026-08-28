namespace TempoCue.Vision;

/// <summary>Сохраняет прежний управляемый алгоритм как заменяемую базовую реализацию.</summary>
internal sealed class ManagedMinimapChampionRecognizer : IMinimapChampionRecognizer
{
    public MinimapChampionRecognitionResult Recognize(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<MinimapChampionReference> references)
    {
        var matches = MinimapChampionIdentityMatcher.Match(
            pixels.Span,
            width,
            height,
            stride,
            candidates,
            references,
            out var maximumObservedSimilarity);

        return new MinimapChampionRecognitionResult(matches, maximumObservedSimilarity);
    }
}
