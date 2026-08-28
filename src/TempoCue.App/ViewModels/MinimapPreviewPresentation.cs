using System.Windows.Media;
using System.Windows.Media.Imaging;
using TempoCue.Vision;

namespace TempoCue.App.ViewModels;

/// <summary>Неизменяемая WPF-проекция редкого диагностического кадра миникарты.</summary>
internal sealed record MinimapPreviewPresentation(
    DateTimeOffset? CapturedAt,
    ImageSource? Image,
    IReadOnlyList<MinimapMarkerOverlayViewModel> MarkerOverlays)
{
    public static MinimapPreviewPresentation Empty { get; } =
        new(null, null, Array.Empty<MinimapMarkerOverlayViewModel>());
}

/// <summary>Изолирует декодирование диагностического кадра от состояния ViewModel.</summary>
internal static class MinimapPreviewPresentationFactory
{
    private static readonly Brush AllyMarkerBrush = CreateFrozenBrush(80, 183, 237);
    private static readonly Brush EnemyMarkerBrush = CreateFrozenBrush(255, 89, 116);

    public static MinimapPreviewPresentation Create(MinimapPreviewSnapshot? preview)
    {
        if (preview is null)
        {
            return MinimapPreviewPresentation.Empty;
        }

        var image = BitmapSource.Create(
            preview.ImageWidth,
            preview.ImageHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            preview.BgraPixels,
            preview.ImageStride);
        image.Freeze();

        var markerOverlays = preview.ChampionMarkerCandidates
            .Select(candidate => new MinimapMarkerOverlayViewModel(
                candidate.X,
                candidate.Y,
                candidate.Width,
                candidate.Height,
                candidate.Affiliation == MinimapMarkerAffiliation.Ally
                    ? AllyMarkerBrush
                    : EnemyMarkerBrush,
                candidate.ChampionId,
                candidate.IdentityConfidence))
            .ToArray();

        return new MinimapPreviewPresentation(preview.CapturedAt, image, markerOverlays);
    }

    private static Brush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
