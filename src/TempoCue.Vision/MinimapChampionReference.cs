namespace TempoCue.Vision;

/// <summary>
/// Нормализованный портрет одного участника активного матча без зависимости от Riot DTO.
/// </summary>
public sealed class MinimapChampionReference
{
    public const int ImageSide = 17;
    public const int BytesPerPixel = 4;

    private readonly byte[] _bgraPixels;

    public MinimapChampionReference(
        string championId,
        MinimapMarkerAffiliation affiliation,
        ReadOnlySpan<byte> bgraPixels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(championId);
        if (!Enum.IsDefined(affiliation))
        {
            throw new ArgumentOutOfRangeException(nameof(affiliation));
        }

        var expectedLength = checked(ImageSide * ImageSide * BytesPerPixel);
        if (bgraPixels.Length != expectedLength)
        {
            throw new ArgumentException(
                "Эталонный портрет должен иметь размер 17 × 17 пикселей BGRA32.",
                nameof(bgraPixels));
        }

        ChampionId = championId;
        Affiliation = affiliation;
        _bgraPixels = bgraPixels.ToArray();
    }

    public string ChampionId { get; }

    public MinimapMarkerAffiliation Affiliation { get; }

    public ReadOnlyMemory<byte> BgraPixels => _bgraPixels;
}
