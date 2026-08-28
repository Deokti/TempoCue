using TempoCue.Vision;
using TempoCue.Vision.OwnModel;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Измеряет устойчивость распознавания между кадрами.
///
/// Корпус независимых кадров отвечает, скольких участников распознаватель находит,
/// но не отличает того, кто находит их на каждом кадре, от того, кто находит через
/// раз. Игроку разница видна сразу: отметка либо держится на чемпионе, либо
/// прыгает. Кроме того, подтверждение появления требует двух наблюдений с разрывом
/// не более 600 мс, поэтому пропуск через кадр прямо удваивает задержку
/// уведомления.
///
/// Метрика простая: доля кадров записи, на которых участник подтверждён, при
/// условии что он виден на карте всю запись.
/// </summary>
internal static class SequenceStability
{
    public static async Task<int> RunAsync(string sequencePath)
    {
        if (!File.Exists(sequencePath))
        {
            Console.Error.WriteLine($"Последовательность не найдена: {sequencePath}");
            return 1;
        }

        await using var stream = File.OpenRead(sequencePath);
        var sequence = await MinimapReplaySequenceStore.LoadAsync(stream);
        var truth = await MinimapReplaySequenceStore.TryLoadGroundTruthAsync(sequencePath);

        Console.WriteLine($"=== {Path.GetFileNameWithoutExtension(sequencePath)} ===");
        Console.WriteLine(
            $"кадров: {sequence.Frames.Count}, длительность: " +
            $"{sequence.Duration.TotalSeconds:F1} с, эталонов: " +
            $"{sequence.ChampionReferences.Count}");

        if (truth is null)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Нет разметки. Рядом с записью нужен файл с тем же именем и суффиксом " +
                $"{MinimapReplaySequenceStore.SidecarSuffix}, перечисляющий тех, кто виден " +
                "всю запись:");
            Console.Error.WriteLine(
                """
                {
                  "schemaVersion": 1,
                  "visibleThroughout": ["Nasus", "Kayle"],
                  "notes": "чем примечательна запись"
                }
                """);
            return 1;
        }

        Console.WriteLine($"видны всю запись: {string.Join(", ", truth.VisibleThroughout)}");
        Console.WriteLine();

        var runners = new List<(string Name, Func<MinimapReplaySample, IReadOnlyList<MinimapChampionMarkerCandidate>> Recognize)>();
        var ourRunner = new MinimapReplayRunner();
        runners.Add(("наш конвейер", sample => ourRunner.Run(sample).Actual.ChampionCandidates));

        // Свою модель показываем всегда, когда веса лежат рядом: пункт 5
        // договора о приёмке — устойчивость между кадрами — единственный,
        // который нельзя подменить полнотой по корпусу.
        OwnModelMinimapRecognizer? ownModel = null;
        if (OwnModelFrameAnalyzer.FindModelDirectory() is { } ownModelDirectory)
        {
            ownModel = new OwnModelMinimapRecognizer(ownModelDirectory);
            runners.Add(("своя модель", sample => ownModel.Recognize(
                sample.BgraPixels.Span,
                sample.ImageWidth,
                sample.ImageHeight,
                sample.ImageStride,
                sample.ChampionReferences)));
        }

        try
        {
            foreach (var (name, recognize) in runners)
            {
                var hits = truth.VisibleThroughout.ToDictionary(
                    champion => champion,
                    _ => 0,
                    StringComparer.OrdinalIgnoreCase);
                var longestGaps = truth.VisibleThroughout.ToDictionary(
                    champion => champion,
                    _ => 0,
                    StringComparer.OrdinalIgnoreCase);
                var currentGaps = truth.VisibleThroughout.ToDictionary(
                    champion => champion,
                    _ => 0,
                    StringComparer.OrdinalIgnoreCase);

                for (var index = 0; index < sequence.Frames.Count; index++)
                {
                    var confirmed = recognize(sequence.ToSample(index))
                        .Where(candidate => candidate.ChampionId is not null)
                        .Select(candidate => candidate.ChampionId!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var champion in truth.VisibleThroughout)
                    {
                        if (confirmed.Contains(champion))
                        {
                            hits[champion]++;
                            currentGaps[champion] = 0;
                            continue;
                        }

                        currentGaps[champion]++;
                        longestGaps[champion] = Math.Max(
                            longestGaps[champion],
                            currentGaps[champion]);
                    }
                }

                Console.WriteLine($"--- {name} ---");
                Console.WriteLine("чемпион          | подтверждён | доля  | худший пропуск подряд");
                foreach (var champion in truth.VisibleThroughout)
                {
                    var share = hits[champion] * 100d / sequence.Frames.Count;
                    Console.WriteLine(
                        $"{champion,-16} | {hits[champion],4} из {sequence.Frames.Count,-4} | " +
                        $"{share,5:F1} % | {longestGaps[champion],3} кадр(ов)");
                }

                var overall = hits.Values.Sum() * 100d /
                    (sequence.Frames.Count * Math.Max(1, truth.VisibleThroughout.Count));
                Console.WriteLine($"устойчивость в целом: {overall:F1} %");
                Console.WriteLine();
            }
        }
        finally
        {
            ownModel?.Dispose();
        }

        Console.WriteLine(
            "Худший пропуск подряд важнее средней доли: подтверждение появления " +
            "требует двух наблюдений с разрывом не более 600 мс, и два пропущенных " +
            "кадра подряд при восьми кадрах в секунду уже съедают четверть окна.");
        return 0;
    }
}
