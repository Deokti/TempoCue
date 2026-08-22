using OpenCvSharp;

namespace TempoCue.Vision.DeeperLeague;

/// <summary>
/// Отбраковывает предсказания, которые сильнее похожи на статичный объект интерфейса,
/// чем на портрет чемпиона. Переиспользует те же встроенные отрицательные эталоны,
/// что дают основному конвейеру ноль ложных подтверждений на корпусе.
/// </summary>
internal sealed class StaticObjectFilter : IDisposable
{
    private const int BytesPerPixel = 4;
    private const int AlignmentRadius = 6;
    private const double StructureWeight = 0.4;
    private const double ColorWeight = 0.6;
    private static readonly int[] TemplateSides = [9, 11, 13, 15];

    private readonly Mat[][] _negatives;
    private bool _isDisposed;

    public StaticObjectFilter()
    {
        _negatives = MinimapNegativeReferenceCatalog.LoadBgraTemplates()
            .Select(CreateScaledTemplates)
            .ToArray();
    }

    /// <summary>
    /// Сравнивает область со статичными объектами и с портретом названного моделью
    /// чемпиона. Абсолютный порог здесь не работает: у настоящих портретов сходство
    /// со статикой бывает выше, чем у ложной области. Значение имеет только то,
    /// что перевешивает — объект интерфейса или сам портрет.
    /// </summary>
    public bool ResemblesStaticObjectMoreThanPortrait(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int stride,
        int centerX,
        int centerY,
        MinimapChampionReference portrait)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(portrait);

        var buffer = new byte[stride * height];
        pixels.CopyTo(buffer);
        using var bgra = Mat.FromPixelData(height, width, MatType.CV_8UC4, buffer, stride);
        using var source = new Mat();
        Cv2.CvtColor(bgra, source, ColorConversionCodes.BGRA2BGR);

        var staticSimilarity = 0d;
        foreach (var templates in _negatives)
        {
            staticSimilarity = Math.Max(
                staticSimilarity,
                FindBestSimilarity(source, centerX, centerY, templates));
        }

        using var portraitTemplates = new TemplateSet(
            CreateScaledTemplates(portrait.BgraPixels.ToArray()));
        var portraitSimilarity = FindBestSimilarity(
            source,
            centerX,
            centerY,
            portraitTemplates.Templates);
        return staticSimilarity >= portraitSimilarity;
    }

    private sealed class TemplateSet(Mat[] templates) : IDisposable
    {
        public Mat[] Templates { get; } = templates;

        public void Dispose()
        {
            foreach (var template in Templates)
            {
                template.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        foreach (var templates in _negatives)
        {
            foreach (var template in templates)
            {
                template.Dispose();
            }
        }
    }

    private static Mat[] CreateScaledTemplates(byte[] bgraTemplate)
    {
        const int side = MinimapChampionReference.ImageSide;
        const int cropSide = 11;
        const int cropOffset = (side - cropSide) / 2;
        using var bgra = Mat.FromPixelData(side, side, MatType.CV_8UC4, bgraTemplate);
        using var full = new Mat();
        Cv2.CvtColor(bgra, full, ColorConversionCodes.BGRA2BGR);
        using var center = new Mat(full, new Rect(cropOffset, cropOffset, cropSide, cropSide));
        return TemplateSides
            .Select(templateSide =>
            {
                var scaled = new Mat();
                Cv2.Resize(
                    center,
                    scaled,
                    new Size(templateSide, templateSide),
                    0,
                    0,
                    templateSide < cropSide ? InterpolationFlags.Area : InterpolationFlags.Linear);
                return scaled;
            })
            .ToArray();
    }

    private static double FindBestSimilarity(
        Mat source,
        int centerX,
        int centerY,
        IReadOnlyList<Mat> templates)
    {
        var best = 0d;
        foreach (var template in templates)
        {
            var half = (template.Width - 1) / 2;
            var searchX = Math.Max(0, centerX - half - AlignmentRadius);
            var searchY = Math.Max(0, centerY - half - AlignmentRadius);
            var searchRight = Math.Min(source.Cols, centerX + half + AlignmentRadius + 1);
            var searchBottom = Math.Min(source.Rows, centerY + half + AlignmentRadius + 1);
            if (searchRight - searchX < template.Width ||
                searchBottom - searchY < template.Height)
            {
                continue;
            }

            using var region = new Mat(
                source,
                new Rect(searchX, searchY, searchRight - searchX, searchBottom - searchY));
            using var structureMap = new Mat();
            Cv2.MatchTemplate(region, template, structureMap, TemplateMatchModes.CCoeffNormed);
            using var colorMap = new Mat();
            Cv2.MatchTemplate(region, template, colorMap, TemplateMatchModes.SqDiffNormed);
            for (var y = 0; y < structureMap.Rows; y++)
            {
                for (var x = 0; x < structureMap.Cols; x++)
                {
                    var structure = (Math.Clamp(structureMap.At<float>(y, x), -1, 1) + 1) / 2;
                    var color = 1 - Math.Clamp(colorMap.At<float>(y, x), 0, 1);
                    best = Math.Max(best, (structure * StructureWeight) + (color * ColorWeight));
                }
            }
        }

        return best;
    }
}
