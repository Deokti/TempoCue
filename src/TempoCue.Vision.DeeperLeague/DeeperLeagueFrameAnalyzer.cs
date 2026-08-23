namespace TempoCue.Vision.DeeperLeague;

/// <summary>
/// Подключает стороннюю модель к живому захвату вместо штатного конвейера.
/// Модель сама находит значки, поэтому цветные контуры и области сравнения не
/// создаются; диагностика показывает только подтверждённые рамки.
/// </summary>
public sealed class DeeperLeagueFrameAnalyzer : IMinimapFrameAnalyzer, IDisposable
{
    private readonly DeeperLeagueMinimapRecognizer _recognizer;
    private bool _isDisposed;

    public DeeperLeagueFrameAnalyzer(string modelDirectory)
        : this(modelDirectory, null)
    {
    }

    /// <param name="maximumDegreeOfParallelism">
    /// Настройка числа потоков распознавания. Она обязана управлять и моделью:
    /// ONNX Runtime иначе создаёт собственный пул по числу ядер, и расход
    /// процессора перестаёт зависеть от выбора пользователя.
    /// </param>
    public DeeperLeagueFrameAnalyzer(string modelDirectory, int? maximumDegreeOfParallelism)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDirectory);
        _recognizer = new DeeperLeagueMinimapRecognizer(
            Path.Combine(modelDirectory, "best.onnx"),
            Path.Combine(modelDirectory, "champMap.json"),
            maximumDegreeOfParallelism: maximumDegreeOfParallelism);
    }

    /// <summary>
    /// Ищет каталог модели рядом с приложением или выше по дереву. Возвращает null,
    /// если весов нет: приложение должно молча остаться на штатном распознавателе,
    /// а не падать из-за отсутствия экспериментальных файлов.
    /// </summary>
    public static string? FindModelDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            foreach (var candidate in new[]
            {
                Path.Combine(directory.FullName, "deeperleague"),
                Path.Combine(directory.FullName, "models", "deeperleague")
            })
            {
                if (File.Exists(Path.Combine(candidate, "best.onnx")) &&
                    File.Exists(Path.Combine(candidate, "champMap.json")))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    public MinimapFrameAnalysis Analyze(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionReference> championReferences)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (championReferences.Count == 0)
        {
            return MinimapFrameAnalysis.Empty;
        }

        var candidates = _recognizer.Recognize(
            pixels.Span,
            width,
            height,
            stride,
            championReferences);
        return new MinimapFrameAnalysis(
            [],
            candidates.Length,
            candidates,
            candidates.Length == 0
                ? 0
                : candidates.Max(static candidate => candidate.IdentityConfidence));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _recognizer.Dispose();
    }
}
