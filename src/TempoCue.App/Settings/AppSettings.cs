namespace TempoCue.App.Settings;

/// <summary>Настройки, которые сохраняются между запусками TempoCue.</summary>
public sealed record AppSettings
{
    public string Language { get; init; } = "ru-RU";

    public OverlayUserSettings Overlay { get; init; } = new();

    /// <summary>
    /// Каким распознавателем разбирать миникарту. Работает только при наличии
    /// файлов выбранной модели рядом с приложением; иначе TempoCue молча
    /// остаётся на штатном распознавателе.
    /// </summary>
    public MinimapRecognizerChoice MinimapRecognizer { get; init; }

    /// <summary>
    /// Прежняя галочка «экспериментальное распознавание». Оставлена только для
    /// переноса сохранённых настроек: в файлах пользователей она уже лежит, и
    /// молча потерять их выбор нельзя. Читается при загрузке, не пишется.
    /// </summary>
    public bool UseExperimentalMinimapRecognizer { get; init; }

    /// <summary>
    /// Сколько областей кадра распознавание сравнивает одновременно. Ноль означает
    /// «выбрать по машине»: половину логических процессоров. На результат
    /// распознавания не влияет — только на время разбора кадра, а через него на
    /// задержку уведомлений и на долю процессора, отнятую у игры.
    /// </summary>
    public int MinimapRecognitionThreads { get; init; }
}
