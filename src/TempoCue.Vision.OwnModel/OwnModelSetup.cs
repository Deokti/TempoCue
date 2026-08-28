using System.Text.Json;
using System.Text.Json.Serialization;

namespace TempoCue.Vision.OwnModel;

/// <summary>
/// Описание модели из <c>model.json</c>: словарь классов и все числа, от которых
/// зависит разбор кадра.
/// </summary>
/// <remarks>
/// Ни одно из этих чисел не задаётся здесь константой намеренно. Порог вершины,
/// шаг карты центров, сторона выреза — свойства конкретных весов, а не кода. Если
/// продублировать их в C#, то при следующем обучении файлы и код разойдутся молча:
/// модель продолжит работать и просто станет хуже. Такую ошибку в этой программе
/// уже ловили, и обнаружилась она много позже появления.
/// </remarks>
public sealed record OwnModelSetup(
    [property: JsonPropertyName("detector")] string DetectorFile,
    [property: JsonPropertyName("identity")] string IdentityFile,
    [property: JsonPropertyName("frameSide")] int FrameSide,
    [property: JsonPropertyName("stride")] int Stride,
    [property: JsonPropertyName("peakThreshold")] double PeakThreshold,
    [property: JsonPropertyName("matchDistance")] double MatchDistance,
    [property: JsonPropertyName("cropSide")] int CropSide,
    [property: JsonPropertyName("rejectIndex")] int RejectIndex,
    [property: JsonPropertyName("rejectName")] string RejectName,
    [property: JsonPropertyName("channelOrder")] string ChannelOrder,
    [property: JsonPropertyName("vocabulary")] IReadOnlyList<string> Vocabulary)
{
    /// <summary>Порядок каналов, который умеет подавать распознаватель.</summary>
    private const string SupportedChannelOrder = "bgra";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static OwnModelSetup Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Описание модели не найдено: {path}");
        }

        var setup = JsonSerializer.Deserialize<OwnModelSetup>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"Пустое описание модели: {path}");
        setup.Validate(path);
        return setup;
    }

    /// <summary>
    /// Проверяет описание на осмысленность до первого кадра. Молчаливое согласие
    /// на нелепые числа стоит дороже отказа запуститься: кадры пойдут, ответы
    /// будут, и разбираться придётся по итогам приёмки.
    /// </summary>
    private void Validate(string path)
    {
        if (!string.Equals(ChannelOrder, SupportedChannelOrder, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"{path}: модель ждёт порядок каналов «{ChannelOrder}», " +
                $"распознаватель умеет подавать только «{SupportedChannelOrder}».");
        }

        if (FrameSide != MinimapFrameNormalizer.CanonicalSize)
        {
            throw new InvalidDataException(
                $"{path}: модель обучена на кадре {FrameSide}, " +
                $"а канонический кадр приложения — {MinimapFrameNormalizer.CanonicalSize}.");
        }

        if (Stride <= 0 || CropSide <= 0 || FrameSide % Stride != 0)
        {
            throw new InvalidDataException(
                $"{path}: шаг карты центров {Stride} и сторона выреза {CropSide} несовместимы " +
                $"со стороной кадра {FrameSide}.");
        }

        if (Vocabulary.Count == 0)
        {
            throw new InvalidDataException($"{path}: пустой словарь классов.");
        }

        if (RejectIndex < 0 || RejectIndex >= Vocabulary.Count)
        {
            throw new InvalidDataException(
                $"{path}: индекс отказа {RejectIndex} вне словаря из {Vocabulary.Count} классов.");
        }

        if (!string.Equals(Vocabulary[RejectIndex], RejectName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{path}: класс отказа по индексу {RejectIndex} назван " +
                $"«{Vocabulary[RejectIndex]}», а не «{RejectName}».");
        }
    }
}
