namespace TempoCue.Vision.DeeperLeague;

/// <summary>Одно предсказание детектора в пикселях входа сети.</summary>
public sealed record YoloDetection(
    int ClassIndex,
    double Confidence,
    double CenterX,
    double CenterY,
    double Width,
    double Height);

/// <summary>
/// Разбирает сырой выход YOLOv8. Модель экспортирована без встроенного подавления
/// перекрытий, поэтому отбор по уверенности и NMS выполняются здесь.
/// </summary>
public static class YoloDetectionDecoder
{
    public static IReadOnlyList<YoloDetection> Decode(
        ReadOnlySpan<float> output,
        int attributeCount,
        int predictionCount,
        double minimumConfidence,
        double maximumOverlap)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attributeCount, 5);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(predictionCount);
        if (output.Length < attributeCount * predictionCount)
        {
            throw new ArgumentException(
                "Размер выходного тензора не соответствует заявленной форме.",
                nameof(output));
        }

        // Выход имеет форму [1, attributeCount, predictionCount]: сначала четыре
        // координаты рамки, затем оценка каждого класса. Значения одного признака
        // лежат подряд, поэтому индекс считается как признак * количество + номер.
        var classCount = attributeCount - 4;
        var detections = new List<YoloDetection>();
        for (var prediction = 0; prediction < predictionCount; prediction++)
        {
            var bestClass = -1;
            var bestScore = 0d;
            for (var classIndex = 0; classIndex < classCount; classIndex++)
            {
                var score = output[((4 + classIndex) * predictionCount) + prediction];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = classIndex;
                }
            }

            if (bestClass < 0 || bestScore < minimumConfidence)
            {
                continue;
            }

            detections.Add(new YoloDetection(
                bestClass,
                bestScore,
                output[prediction],
                output[predictionCount + prediction],
                output[(2 * predictionCount) + prediction],
                output[(3 * predictionCount) + prediction]));
        }

        return SuppressOverlapping(detections, maximumOverlap);
    }

    /// <summary>
    /// Оставляет по одной рамке на объект. Подавление выполняется внутри класса:
    /// два разных чемпиона рядом должны сохранить обе рамки, а дубли одного —
    /// схлопнуться. Это отличается от подавления по всем классам сразу и важно
    /// именно для слипшихся значков.
    /// </summary>
    private static List<YoloDetection> SuppressOverlapping(
        List<YoloDetection> detections,
        double maximumOverlap)
    {
        var ordered = detections
            .OrderByDescending(static detection => detection.Confidence)
            .ThenBy(static detection => detection.ClassIndex)
            .ToArray();
        var kept = new List<YoloDetection>(ordered.Length);
        foreach (var detection in ordered)
        {
            var isSuppressed = kept.Any(existing =>
                existing.ClassIndex == detection.ClassIndex &&
                IntersectionOverUnion(existing, detection) > maximumOverlap);
            if (!isSuppressed)
            {
                kept.Add(detection);
            }
        }

        return kept;
    }

    private static double IntersectionOverUnion(YoloDetection first, YoloDetection second)
    {
        var firstLeft = first.CenterX - (first.Width / 2);
        var firstTop = first.CenterY - (first.Height / 2);
        var firstRight = firstLeft + first.Width;
        var firstBottom = firstTop + first.Height;
        var secondLeft = second.CenterX - (second.Width / 2);
        var secondTop = second.CenterY - (second.Height / 2);
        var secondRight = secondLeft + second.Width;
        var secondBottom = secondTop + second.Height;

        var overlapWidth = Math.Min(firstRight, secondRight) - Math.Max(firstLeft, secondLeft);
        var overlapHeight = Math.Min(firstBottom, secondBottom) - Math.Max(firstTop, secondTop);
        if (overlapWidth <= 0 || overlapHeight <= 0)
        {
            return 0;
        }

        var overlap = overlapWidth * overlapHeight;
        var union = (first.Width * first.Height) + (second.Width * second.Height) - overlap;
        return union <= 0 ? 0 : overlap / union;
    }
}
