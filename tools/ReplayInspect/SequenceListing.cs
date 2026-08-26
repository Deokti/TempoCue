using TempoCue.Vision;
using TempoCue.Vision.DeeperLeague;
using TempoCue.Vision.OwnModel;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Показывает покадровую картину подтверждений: кто подтверждён на каком кадре.
/// Нужен до разметки — чтобы увидеть, кто держится всю запись, а кто мигает, и
/// составить `visibleThroughout` не на глаз, а по наблюдению.
///
/// Таблица намеренно рисуется знаками, а не числами: мигание видно как разрыв в
/// строке, и это читается быстрее любой доли.
/// </summary>
internal static class SequenceListing
{
    public static async Task<int> RunAsync(string sequencePath, string? modelDirectory)
    {
        if (!File.Exists(sequencePath))
        {
            Console.Error.WriteLine($"Последовательность не найдена: {sequencePath}");
            return 1;
        }

        await using var stream = File.OpenRead(sequencePath);
        var sequence = await MinimapReplaySequenceStore.LoadAsync(stream);

        Console.WriteLine($"=== {Path.GetFileNameWithoutExtension(sequencePath)} ===");
        Console.WriteLine(
            $"кадров: {sequence.Frames.Count}, длительность: " +
            $"{sequence.Duration.TotalSeconds:F1} с");
        Console.WriteLine();

        var runner = new MinimapReplayRunner();
        PrintTable(
            "наш конвейер",
            sequence,
            index => runner.Run(sequence.ToSample(index)).Actual.ChampionCandidates);

        if (modelDirectory is not null)
        {
            using var model = new DeeperLeagueMinimapRecognizer(
                Path.Combine(modelDirectory, "best.onnx"),
                Path.Combine(modelDirectory, "champMap.json"));
            PrintTable(
                "DeeperLeague",
                sequence,
                index =>
                {
                    var sample = sequence.ToSample(index);
                    return model.Recognize(
                        sample.BgraPixels.Span,
                        sample.ImageWidth,
                        sample.ImageHeight,
                        sample.ImageStride,
                        sample.ChampionReferences);
                });
        }

        // Свою модель искать не просят: если веса лежат рядом, показываем её
        // всегда. Пункт 6 договора о приёмке — разброс положений — иначе
        // остаётся неизмеренным просто потому, что о ключе забыли.
        if (OwnModelFrameAnalyzer.FindModelDirectory() is { } ownModelDirectory)
        {
            using var ownModel = new OwnModelMinimapRecognizer(ownModelDirectory);
            PrintTable(
                "своя модель",
                sequence,
                index =>
                {
                    var sample = sequence.ToSample(index);
                    return ownModel.Recognize(
                        sample.BgraPixels.Span,
                        sample.ImageWidth,
                        sample.ImageHeight,
                        sample.ImageStride,
                        sample.ChampionReferences);
                });
        }

        return 0;
    }

    private static double Distance((double X, double Y) first, (double X, double Y) second) =>
        Math.Sqrt(
            ((first.X - second.X) * (first.X - second.X)) +
            ((first.Y - second.Y) * (first.Y - second.Y)));

    private static void PrintTable(
        string title,
        MinimapReplaySequence sequence,
        Func<int, IReadOnlyList<MinimapChampionMarkerCandidate>> recognize)
    {
        var perFrame = new List<Dictionary<string, (double X, double Y)>>(sequence.Frames.Count);
        for (var index = 0; index < sequence.Frames.Count; index++)
        {
            perFrame.Add(recognize(index)
                .Where(candidate => candidate.ChampionId is not null)
                .GroupBy(candidate => candidate.ChampionId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (
                        X: group.First().X + (group.First().Width / 2d),
                        Y: group.First().Y + (group.First().Height / 2d)),
                    StringComparer.OrdinalIgnoreCase));
        }

        var champions = sequence.ChampionReferences
            .Select(reference => reference.ChampionId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(champion => perFrame.Any(frame => frame.ContainsKey(champion)))
            .OrderByDescending(champion => perFrame.Count(frame => frame.ContainsKey(champion)))
            .ToArray();

        Console.WriteLine($"--- {title} ---");
        Console.WriteLine($"{"чемпион",-16} | {"кадры 1..N",-24} | подтверждён");
        foreach (var champion in champions)
        {
            var row = string.Concat(perFrame.Select(frame => frame.ContainsKey(champion) ? '#' : '.'));
            var hits = perFrame.Count(frame => frame.ContainsKey(champion));
            // Далеко разнесённые места у одного чемпиона в одной записи означают,
            // что часть подтверждений досталась чему-то другому.
            var places = perFrame
                .Where(frame => frame.ContainsKey(champion))
                .Select(frame => frame[champion])
                .ToArray();
            var spread = places.Length < 2
                ? 0
                : places.Max(place => places.Max(other => Distance(place, other)));
            Console.WriteLine(
                $"{champion,-16} | {row,-24} | {hits,2} из {perFrame.Count} " +
                $"({hits * 100d / perFrame.Count:F0} %) | разброс {spread,3:F0} пкс");
        }

        var unseen = sequence.ChampionReferences
            .Select(reference => reference.ChampionId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(champion => !champions.Contains(champion, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (unseen.Length > 0)
        {
            Console.WriteLine($"ни разу не подтверждены: {string.Join(", ", unseen)}");
        }

        Console.WriteLine();
    }
}
