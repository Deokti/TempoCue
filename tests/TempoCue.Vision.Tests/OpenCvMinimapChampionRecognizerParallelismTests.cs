using TempoCue.Vision;

namespace TempoCue.Vision.Tests;

/// <summary>
/// Число потоков сравнения — настройка производительности, а не распознавания.
/// Тест закрепляет это свойство: на каждом кадре корпуса последовательный и
/// параллельный проходы обязаны давать посимвольно и побитово одинаковый ответ.
/// Без такой проверки ускорение нельзя отличить от тихой порчи результата.
/// </summary>
public sealed class OpenCvMinimapChampionRecognizerParallelismTests
{
    private static readonly string CorpusDirectory =
        Path.Combine(AppContext.BaseDirectory, "ReplayCorpus");

    [Fact]
    public async Task DegreeOfParallelism_DoesNotChangeRecognition()
    {
        if (!Directory.Exists(CorpusDirectory))
        {
            return;
        }

        using var sequential = new OpenCvMinimapChampionRecognizer(1);
        using var parallel = new OpenCvMinimapChampionRecognizer(8);
        var sequentialRunner = new MinimapReplayRunner(sequential);
        var parallelRunner = new MinimapReplayRunner(parallel);

        var samplePaths = Directory
            .EnumerateFiles(CorpusDirectory, "*.tempocue-vision", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(samplePaths);

        foreach (var samplePath in samplePaths)
        {
            await using var stream = File.OpenRead(samplePath);
            var sample = await MinimapReplaySampleStore.LoadAsync(stream);

            var sequentialOutcome = sequentialRunner.Run(sample).Actual;
            var parallelOutcome = parallelRunner.Run(sample).Actual;
            var sampleName = Path.GetFileName(samplePath);

            Assert.Equal(
                sequentialOutcome.ChampionProposalCount,
                parallelOutcome.ChampionProposalCount);
            Assert.Equal(
                sequentialOutcome.MaximumIdentityConfidence,
                parallelOutcome.MaximumIdentityConfidence);
            Assert.Equal(
                Describe(sequentialOutcome.ChampionCandidates, sampleName),
                Describe(parallelOutcome.ChampionCandidates, sampleName));
        }
    }

    /// <summary>
    /// Сравниваются не только личности и места, но и сами баллы: сдвиг балла на
    /// последнем знаке означает, что порядок вычислений перестал быть прежним.
    /// </summary>
    private static string Describe(
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        string sampleName) =>
        string.Join(
            Environment.NewLine,
            candidates.Select(candidate =>
                $"{sampleName}: {candidate.ChampionId} [{candidate.Affiliation}] " +
                $"({candidate.X},{candidate.Y}) " +
                $"{candidate.IdentityConfidence:R} {candidate.Confidence:R}"));
}
