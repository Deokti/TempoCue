using OpenCvSharp;
using TempoCue.Vision.DeeperLeague;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Сравнивает значки чемпионов, на которых обучалась сторонняя модель, с актуальными
/// значками Riot. Модель обучена на замороженном наборе, поэтому перерисованные
/// значки она перестаёт узнавать так же, как совсем новых чемпионов.
/// </summary>
internal static class ChampionIconComparison
{
    private const int ComparisonSide = 32;
    private const string ModelIconRoot =
        "https://raw.githubusercontent.com/davidweatherall/DeeperLeague/main/champions";

    public static async Task<int> RunAsync(string modelDirectory, string dataDragonVersion)
    {
        var mapPath = Path.Combine(modelDirectory, "champMap.json");
        if (!File.Exists(mapPath))
        {
            Console.Error.WriteLine($"Не найдена карта чемпионов модели: {mapPath}");
            return 1;
        }

        var map = DeeperLeagueChampionMap.Load(mapPath);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var results = new List<IconDifference>();
        var unavailable = new List<string>();

        Console.WriteLine(
            $"Сравнение {map.Count} значков модели с Data Dragon {dataDragonVersion}...");
        for (var index = 0; index < map.Count; index++)
        {
            if (map[index] is not { } championId)
            {
                continue;
            }

            var modelIcon = await TryLoadAsync(
                client,
                $"{ModelIconRoot}/{Uri.EscapeDataString(championId)}/1.png");
            var currentIcon = await TryLoadAsync(
                client,
                $"https://ddragon.leagueoflegends.com/cdn/{dataDragonVersion}" +
                $"/img/champion/{Uri.EscapeDataString(championId)}.png");
            if (modelIcon is null || currentIcon is null)
            {
                unavailable.Add(championId);
                modelIcon?.Dispose();
                currentIcon?.Dispose();
                continue;
            }

            using (modelIcon)
            using (currentIcon)
            {
                results.Add(new IconDifference(championId, Compare(modelIcon, currentIcon)));
            }
        }

        var ordered = results.OrderByDescending(static result => result.Difference).ToArray();
        Console.WriteLine();
        Console.WriteLine("=== НАИБОЛЬШИЕ РАСХОЖДЕНИЯ ЗНАЧКОВ ===");
        Console.WriteLine("чемпион          | различие");
        foreach (var result in ordered.Take(30))
        {
            Console.WriteLine($"{result.ChampionId,-16} | {result.Difference:F1}");
        }

        Console.WriteLine();
        var median = ordered.Length == 0 ? 0 : ordered[ordered.Length / 2].Difference;
        Console.WriteLine(
            $"медиана различия: {median:F1}, минимум: {ordered.LastOrDefault()?.Difference:F1}");
        if (unavailable.Count > 0)
        {
            Console.WriteLine(
                $"не удалось сравнить ({unavailable.Count}): {string.Join(", ", unavailable)}");
        }

        return 0;
    }

    private static async Task<Mat?> TryLoadAsync(HttpClient client, string url)
    {
        try
        {
            var bytes = await client.GetByteArrayAsync(url);
            var image = Cv2.ImDecode(bytes, ImreadModes.Color);
            return image.Empty() ? null : image;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Среднее абсолютное различие яркости и цвета после приведения обоих значков к
    /// одному размеру. Метрика грубая, но её достаточно, чтобы отделить перерисованный
    /// значок от того же самого, пересжатого другим способом.
    /// </summary>
    private static double Compare(Mat first, Mat second)
    {
        using var firstScaled = new Mat();
        using var secondScaled = new Mat();
        var size = new Size(ComparisonSide, ComparisonSide);
        Cv2.Resize(first, firstScaled, size, 0, 0, InterpolationFlags.Area);
        Cv2.Resize(second, secondScaled, size, 0, 0, InterpolationFlags.Area);
        using var difference = new Mat();
        Cv2.Absdiff(firstScaled, secondScaled, difference);
        return Cv2.Mean(difference).Val0 +
            Cv2.Mean(difference).Val1 +
            Cv2.Mean(difference).Val2;
    }

    /// <summary>
    /// Прямая проверка вместо косвенной метрики: актуальный значок каждого чемпиона
    /// помещается на нейтральный фон в том размере, в каком он появляется на
    /// канонической миникарте, и модели задаётся вопрос, кого она здесь видит.
    /// Отвечает не «насколько значок изменился», а «узнаёт ли его модель».
    /// </summary>
    public static async Task<int> RunRecognitionAsync(
        string modelDirectory,
        string dataDragonVersion)
    {
        var mapPath = Path.Combine(modelDirectory, "champMap.json");
        var map = DeeperLeagueChampionMap.Load(mapPath);
        using var model = new DeeperLeagueMinimapRecognizer(
            Path.Combine(modelDirectory, "best.onnx"),
            mapPath,
            minimumConfidence: 0.20,
            rejectStaticObjects: false);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var unrecognized = new List<string>();
        var recognized = new List<(string ChampionId, double Confidence)>();
        var unavailable = new List<string>();
        Console.WriteLine($"Проверка узнавания {map.Count} значков модели...");

        for (var index = 0; index < map.Count; index++)
        {
            if (map[index] is not { } championId)
            {
                continue;
            }

            var icon = await TryLoadAsync(
                client,
                $"https://ddragon.leagueoflegends.com/cdn/{dataDragonVersion}" +
                $"/img/champion/{Uri.EscapeDataString(championId)}.png");
            if (icon is null)
            {
                unavailable.Add(championId);
                continue;
            }

            using (icon)
            {
                var frame = RenderOnMinimap(icon);
                var reference = new TempoCue.Vision.MinimapChampionReference(
                    championId,
                    TempoCue.Vision.MinimapMarkerAffiliation.Ally,
                    new byte[
                        TempoCue.Vision.MinimapChampionReference.ImageSide *
                        TempoCue.Vision.MinimapChampionReference.ImageSide *
                        TempoCue.Vision.MinimapChampionReference.BytesPerPixel]);
                var found = model.Recognize(frame, 320, 320, 320 * 4, [reference]);
                var match = found.FirstOrDefault(candidate => string.Equals(
                    candidate.ChampionId,
                    championId,
                    StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    unrecognized.Add(championId);
                }
                else
                {
                    recognized.Add((championId, match.IdentityConfidence));
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"=== МОДЕЛЬ НЕ УЗНАЁТ АКТУАЛЬНЫЙ ЗНАЧОК ({unrecognized.Count}) ===");
        foreach (var championId in unrecognized.OrderBy(static id => id, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {championId}");
        }

        Console.WriteLine();
        Console.WriteLine("=== УЗНАЁТ С НАИМЕНЬШЕЙ УВЕРЕННОСТЬЮ ===");
        foreach (var entry in recognized.OrderBy(static entry => entry.Confidence).Take(12))
        {
            Console.WriteLine($"  {entry.ChampionId,-16} {entry.Confidence:F3}");
        }

        Console.WriteLine();
        Console.WriteLine($"итого узнаёт {recognized.Count} из {recognized.Count + unrecognized.Count}");
        if (unavailable.Count > 0)
        {
            Console.WriteLine($"не удалось загрузить: {string.Join(", ", unavailable)}");
        }

        return 0;
    }

    /// <summary>
    /// Воспроизводит значок так, как его рисует игра: круглый вырез портрета в
    /// командном кольце. Без кольца проверка недостоверна — модель обучалась на
    /// значках целиком и голый портрет узнаёт заметно хуже.
    /// </summary>
    private static byte[] RenderOnMinimap(Mat icon)
    {
        const int canvasSide = 320;
        const int iconSide = 25;
        const int placement = (canvasSide - iconSide) / 2;
        const double center = (iconSide - 1) / 2d;
        const double portraitRadius = 10.5;
        const double ringRadius = 12;
        using var scaled = new Mat();
        Cv2.Resize(icon, scaled, new Size(iconSide, iconSide), 0, 0, InterpolationFlags.Area);

        var frame = new byte[canvasSide * canvasSide * 4];
        for (var index = 0; index < frame.Length; index += 4)
        {
            frame[index] = 60;
            frame[index + 1] = 70;
            frame[index + 2] = 55;
            frame[index + 3] = 255;
        }

        for (var y = 0; y < iconSide; y++)
        {
            for (var x = 0; x < iconSide; x++)
            {
                var deltaX = x - center;
                var deltaY = y - center;
                var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                if (distance > ringRadius)
                {
                    continue;
                }

                var offset = (((placement + y) * canvasSide) + placement + x) * 4;
                if (distance > portraitRadius)
                {
                    // Кольцо союзной команды: голубой оттенок, который распознаётся
                    // как командный цвет и нашим детектором контуров.
                    frame[offset] = 225;
                    frame[offset + 1] = 165;
                    frame[offset + 2] = 90;
                }
                else
                {
                    var pixel = scaled.At<Vec3b>(y, x);
                    frame[offset] = pixel.Item0;
                    frame[offset + 1] = pixel.Item1;
                    frame[offset + 2] = pixel.Item2;
                }

                frame[offset + 3] = 255;
            }
        }

        return frame;
    }

    private sealed record IconDifference(string ChampionId, double Difference);
}
