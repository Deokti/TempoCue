namespace TempoCue.App.Settings;

/// <summary>Настройки, которые сохраняются между запусками TempoCue.</summary>
public sealed record AppSettings
{
    public string Language { get; init; } = "ru-RU";

    public OverlayUserSettings Overlay { get; init; } = new();

    /// <summary>
    /// Включает экспериментальное распознавание миникарты сторонней моделью.
    /// Работает только при наличии её файлов рядом с приложением; иначе TempoCue
    /// молча остаётся на штатном распознавателе.
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
