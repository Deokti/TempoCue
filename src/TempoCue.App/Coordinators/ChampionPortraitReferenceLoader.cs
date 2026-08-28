using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using TempoCue.Application;
using TempoCue.Vision;

namespace TempoCue.App.Coordinators;

/// <summary>Загружает и нормализует только портреты участников текущего матча.</summary>
internal sealed class ChampionPortraitReferenceLoader :
    IChampionPortraitReferenceLoader,
    IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };
    private readonly ConcurrentDictionary<Uri, byte[]> _pixelCache = new();

    public async Task<MinimapChampionReference?> LoadAsync(
        ChampionCatalogEntry champion,
        MinimapMarkerAffiliation affiliation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(champion);
        if (_pixelCache.TryGetValue(champion.ImageUri, out var cachedPixels))
        {
            return new MinimapChampionReference(champion.Id, affiliation, cachedPixels);
        }

        var imageBytes = await _httpClient.GetByteArrayAsync(
            champion.ImageUri,
            cancellationToken).ConfigureAwait(false);
        using var imageStream = new MemoryStream(imageBytes, writable: false);
        var pixels = DecodePortrait(imageStream);
        _pixelCache.TryAdd(champion.ImageUri, pixels);
        return new MinimapChampionReference(champion.Id, affiliation, pixels);
    }

    public void Dispose() => _httpClient.Dispose();

    private static byte[] DecodePortrait(Stream imageStream)
    {
        var decoder = BitmapDecoder.Create(
            imageStream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var source = decoder.Frames.FirstOrDefault() ?? throw new InvalidDataException(
            "Data Dragon не вернул кадр портрета чемпиона.");
        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
        {
            throw new InvalidDataException("Портрет чемпиона имеет неверный размер.");
        }

        BitmapSource bgraSource = source;
        if (source.Format != System.Windows.Media.PixelFormats.Bgra32)
        {
            bgraSource = new FormatConvertedBitmap(
                source,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                0);
        }

        var sourceStride = checked(
            bgraSource.PixelWidth * MinimapChampionReference.BytesPerPixel);
        var sourcePixels = GC.AllocateUninitializedArray<byte>(
            checked(sourceStride * bgraSource.PixelHeight));
        bgraSource.CopyPixels(sourcePixels, sourceStride, 0);
        return ChampionPortraitNormalizer.NormalizeBgra32(
            sourcePixels,
            bgraSource.PixelWidth,
            bgraSource.PixelHeight,
            sourceStride);
    }
}
