namespace TempoCue.Vision.Tests;

internal static class MinimapRecognitionTestData
{
    public static MinimapChampionReference CreateReference(
        BgraTestImage image,
        int startX,
        int startY,
        string championId,
        MinimapMarkerAffiliation affiliation = MinimapMarkerAffiliation.Ally)
    {
        var pixels = new byte[
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.BytesPerPixel];
        for (var y = 0; y < MinimapChampionReference.ImageSide; y++)
        {
            var sourceOffset = ((startY + y) * image.Stride) +
                (startX * MinimapChampionReference.BytesPerPixel);
            var targetOffset = y *
                MinimapChampionReference.ImageSide *
                MinimapChampionReference.BytesPerPixel;
            image.Pixels.AsSpan(
                    sourceOffset,
                    MinimapChampionReference.ImageSide * MinimapChampionReference.BytesPerPixel)
                .CopyTo(pixels.AsSpan(targetOffset));
        }

        return new MinimapChampionReference(championId, affiliation, pixels);
    }
}
