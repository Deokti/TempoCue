using System.Text.Json;

namespace TempoCue.Core;

/// <summary>
/// Загружает Coach Pack из JSON и не пропускает в движок некорректные правила.
/// </summary>
public static class CoachPackLoader
{
    /// <summary>Открывает Coach Pack по указанному пути и загружает его.</summary>
    public static CoachPack LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>
    /// Загружает Coach Pack из потока. Отдельный метод с потоком упрощает тестирование:
    /// тест может передать JSON из памяти, не создавая временный файл.
    /// </summary>
    public static CoachPack Load(Stream jsonStream)
    {
        ArgumentNullException.ThrowIfNull(jsonStream);

        // Deserialize превращает текст JSON в типизированный объект CoachPack.
        var pack = JsonSerializer.Deserialize<CoachPack>(jsonStream, CoachPackJson.Options)
            ?? throw new InvalidDataException("Coach Pack is empty.");

        // Проверяем данные один раз на границе системы. После этого движок может
        // считать, что получил правильно сформированный набор правил.
        CoachPackValidator.Validate(pack);
        return pack;
    }
}
