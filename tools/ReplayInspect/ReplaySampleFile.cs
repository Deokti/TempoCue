using TempoCue.Vision;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>Читает сохранённые кадры и их ручную разметку с понятными ошибками.</summary>
internal static class ReplaySampleFile
{
    public const string Extension = "*.tempocue-vision";

    public static async Task<MinimapReplaySample> LoadAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Кадр не найден: {path}");
        }

        await using var stream = File.OpenRead(path);
        return await MinimapReplaySampleStore.LoadAsync(stream);
    }

    public static async Task<MinimapReplayGroundTruth?> TryLoadLabelsAsync(string samplePath)
    {
        var labelsPath = samplePath + MinimapReplayGroundTruthStore.SidecarSuffix;
        if (!File.Exists(labelsPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(labelsPath);
        return await MinimapReplayGroundTruthStore.LoadAsync(stream);
    }

    public static IEnumerable<string> EnumerateCorpus(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new FileNotFoundException($"Каталог не найден: {directory}");
        }

        return Directory
            .EnumerateFiles(directory, Extension, SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);
    }

    public static string Name(string path) => Path.GetFileNameWithoutExtension(path);

    /// <summary>
    /// Переводит размеченную область в левый верхний угол области сравнения так,
    /// чтобы их центры совпали: зонд всегда работает с областью 25 x 25.
    /// </summary>
    public static (int X, int Y)? ToProbeOrigin(MinimapGroundTruthRegion region)
    {
        var x = (int)Math.Round(region.CenterX) - 12;
        var y = (int)Math.Round(region.CenterY) - 12;
        return x < 0 || y < 0 || x > 320 - 25 || y > 320 - 25 ? null : (x, y);
    }
}
