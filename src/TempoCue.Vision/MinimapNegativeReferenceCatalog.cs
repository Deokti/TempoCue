namespace TempoCue.Vision;

/// <summary>
/// Встроенные эталоны статичных объектов интерфейса League — башен, строений базы,
/// меток местности и миньонов. Они не зависят от матча и состава: область, похожая
/// на такой объект не слабее, чем на портрет любого участника, не является чемпионом.
/// </summary>
internal static class MinimapNegativeReferenceCatalog
{
    private const string ResourcePrefix = "TempoCue.Vision.Assets.NegativeReferences.";
    private const string ResourceSuffix = ".bgra";

    public static IReadOnlyList<byte[]> LoadBgraTemplates()
    {
        var assembly = typeof(MinimapNegativeReferenceCatalog).Assembly;
        var expectedLength = checked(
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.BytesPerPixel);
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(static name =>
                name.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var templates = new byte[resourceNames.Length][];
        for (var index = 0; index < resourceNames.Length; index++)
        {
            using var stream = assembly.GetManifestResourceStream(resourceNames[index]) ??
                throw new InvalidOperationException(
                    $"Встроенный эталон '{resourceNames[index]}' недоступен.");
            if (stream.Length != expectedLength)
            {
                throw new InvalidDataException(
                    $"Встроенный эталон '{resourceNames[index]}' имеет неверный размер.");
            }

            var buffer = new byte[expectedLength];
            stream.ReadExactly(buffer);
            templates[index] = buffer;
        }

        return templates;
    }
}
