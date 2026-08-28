namespace TempoCue.Vision;

/// <summary>
/// Измеряет кольцо вокруг предполагаемого значка чемпиона: доли пикселей
/// командных цветов и яркого свечения отзыва либо активного игрока. Сам по себе
/// результат — измерение, а не решение о видимости.
/// </summary>
internal static class MinimapIconRingSampler
{
    private const int BytesPerPixel = 4;

    /// <summary>
    /// Внутренний и внешний радиусы кольца в пикселях канонического кадра.
    /// Значок чемпиона в области предложения 25 × 25 занимает круг диаметром
    /// около 22–24 пикселей, поэтому команда рисует кольцо на радиусе 10–12.
    /// </summary>
    private const double InnerRadius = 9.5;
    private const double OuterRadius = 12.5;

    private const byte EnemyClass = 2;
    private const byte AllyClass = 1;

    /// <summary>Порог «яркого» пикселя свечения: белёсые пиксели кольца отзыва.</summary>
    private const int BrightMinimumChannel = 150;

    public static MinimapRingSampleStats Sample(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int stride,
        MinimapChampionMarkerCandidate proposal)
    {
        var centerX = proposal.X + ((proposal.Width - 1) / 2d);
        var centerY = proposal.Y + ((proposal.Height - 1) / 2d);
        var sampled = 0;
        var enemyColored = 0;
        var allyColored = 0;
        var bright = 0;
        double sumBlue = 0;
        double sumGreen = 0;
        double sumRed = 0;

        var minX = Math.Max(0, (int)Math.Floor(centerX - OuterRadius));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(centerX + OuterRadius));
        var minY = Math.Max(0, (int)Math.Floor(centerY - OuterRadius));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(centerY + OuterRadius));
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var deltaX = x - centerX;
                var deltaY = y - centerY;
                var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                if (distance < InnerRadius || distance > OuterRadius)
                {
                    continue;
                }

                var offset = (y * stride) + (x * BytesPerPixel);
                var blue = pixels[offset];
                var green = pixels[offset + 1];
                var red = pixels[offset + 2];
                sampled++;
                sumBlue += blue;
                sumGreen += green;
                sumRed += red;
                var teamClass = MinimapMarkerCandidateDetector.Classify(blue, green, red);
                if (teamClass == EnemyClass)
                {
                    enemyColored++;
                }
                else if (teamClass == AllyClass)
                {
                    allyColored++;
                }

                if (Math.Min(blue, Math.Min(green, red)) >= BrightMinimumChannel)
                {
                    bright++;
                }
            }
        }

        return new MinimapRingSampleStats(
            sampled,
            enemyColored,
            allyColored,
            bright,
            sampled == 0 ? 0 : sumBlue / sampled,
            sampled == 0 ? 0 : sumGreen / sampled,
            sampled == 0 ? 0 : sumRed / sampled);
    }
}

/// <summary>Сырые измерения кольца значка: счётчики классов пикселей и средний цвет.</summary>
public sealed record MinimapRingSampleStats(
    int SampledPixels,
    int EnemyColored,
    int AllyColored,
    int BrightPixels,
    double AverageBlue,
    double AverageGreen,
    double AverageRed);
