using TempoCue.Vision;

namespace TempoCue.Vision.Tests;

/// <summary>
/// Прогоняет распознавание по корпусу сохранённых кадров с ручной разметкой.
/// Пустой корпус проходит: тесты появляются вместе с файлами `.tempocue-vision`
/// и их сопутствующей разметкой `.labels.json`.
/// </summary>
public sealed class MinimapReplayCorpusTests
{
    private static readonly string CorpusDirectory =
        Path.Combine(AppContext.BaseDirectory, "ReplayCorpus");

    [Fact]
    public async Task CorpusSamples_StayWithinTheirLabeledAllowances()
    {
        if (!Directory.Exists(CorpusDirectory))
        {
            return;
        }

        var failures = new List<string>();
        var runner = new MinimapReplayRunner();
        foreach (var samplePath in Directory
                     .EnumerateFiles(CorpusDirectory, "*.tempocue-vision", SearchOption.AllDirectories)
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            var sampleName = Path.GetFileName(samplePath);
            var labelsPath = samplePath + MinimapReplayGroundTruthStore.SidecarSuffix;
            if (!File.Exists(labelsPath))
            {
                failures.Add($"{sampleName}: отсутствует файл разметки {Path.GetFileName(labelsPath)}");
                continue;
            }

            MinimapReplayEvaluation evaluation;
            MinimapReplayGroundTruth groundTruth;
            try
            {
                (groundTruth, evaluation) = await EvaluateAsync(
                    runner,
                    samplePath,
                    labelsPath);
            }
            catch (InvalidDataException exception)
            {
                failures.Add($"{sampleName}: {exception.Message}");
                continue;
            }

            if (!evaluation.SatisfiesAllowances(groundTruth))
            {
                var explainedFalsePositives = string.Join(
                    ", ",
                    evaluation.FalsePositives.Select(static falsePositive =>
                        falsePositive.NearestRegion?.Kind.ToString() ?? "Unlabeled"));
                failures.Add(
                    $"{sampleName}: ложных подтверждений {evaluation.FalsePositives.Count} " +
                    $"(допуск {groundTruth.AllowedFalsePositives}; {explainedFalsePositives}), " +
                    $"пропущено чемпионов {evaluation.MissedChampions.Count} " +
                    $"(допуск {groundTruth.AllowedMissedChampions})");
            }
        }

        Assert.True(
            failures.Count == 0,
            "Корпус replay-кадров разошёлся с разметкой:\n" + string.Join('\n', failures));
    }

    private static async Task<(
        MinimapReplayGroundTruth GroundTruth,
        MinimapReplayEvaluation Evaluation)> EvaluateAsync(
        MinimapReplayRunner runner,
        string samplePath,
        string labelsPath)
    {
        await using var sampleStream = File.OpenRead(samplePath);
        var sample = await MinimapReplaySampleStore.LoadAsync(sampleStream);
        await using var labelsStream = File.OpenRead(labelsPath);
        var groundTruth = await MinimapReplayGroundTruthStore.LoadAsync(labelsStream);
        var evaluation = MinimapReplayEvaluator.Evaluate(
            runner.Run(sample).Actual,
            groundTruth);
        return (groundTruth, evaluation);
    }
}
