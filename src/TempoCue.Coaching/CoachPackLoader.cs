using System.Text.Json;

namespace TempoCue.Coaching;

/// <summary>
/// Загружает Coach Pack из JSON и не пропускает в движок некорректные правила.
/// </summary>
public static class CoachPackLoader
{
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

    /// <summary>Асинхронно загружает и проверяет Coach Pack из потока внешнего хранилища.</summary>
    public static async Task<CoachPack> LoadAsync(
        Stream jsonStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsonStream);

        var pack = await JsonSerializer.DeserializeAsync<CoachPack>(
            jsonStream,
            CoachPackJson.Options,
            cancellationToken) ?? throw new InvalidDataException("Coach Pack is empty.");
        CoachPackValidator.Validate(pack);
        return pack;
    }
}
