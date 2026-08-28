namespace TempoCue.App.Settings;

/// <summary>Настройки, которые сохраняются между запусками TempoCue.</summary>
public sealed record AppSettings
{
    public string Language { get; init; } = "ru-RU";

    public OverlayUserSettings Overlay { get; init; } = new();

    /// <summary>Что применять, если человек ничего не выбирал.</summary>
    /// <remarks>
    /// Своя модель берёт 64 чемпиона из 67 против 45 у штатного конвейера при
    /// вдвое меньшей доле процессора. Отступление от договора о приёмке и его
    /// обоснование записаны в `docs/own-model-plan.md`.
    /// </remarks>
    public const MinimapRecognizerChoice DefaultMinimapRecognizer =
        MinimapRecognizerChoice.OwnModel;

    /// <summary>
    /// Выбор человека. `null` означает «не выбирал»: тогда применяется
    /// умолчание приложения. Различать выбор и его отсутствие обязательно —
    /// иначе смена умолчания молча переписала бы уже сделанный выбор.
    /// </summary>
    public MinimapRecognizerChoice? MinimapRecognizer { get; init; }

    /// <summary>
    /// Каким распознавателем разбирать миникарту на самом деле. Работает только
    /// при наличии файлов выбранной модели рядом с приложением; иначе TempoCue
    /// молча остаётся на штатном распознавателе.
    /// </summary>
    public MinimapRecognizerChoice EffectiveMinimapRecognizer =>
        MinimapRecognizer ?? DefaultMinimapRecognizer;

    /// <summary>
    /// Сколько областей кадра распознавание сравнивает одновременно. Ноль означает
    /// «выбрать по машине»: половину логических процессоров. На результат
    /// распознавания не влияет — только на время разбора кадра, а через него на
    /// задержку уведомлений и на долю процессора, отнятую у игры.
    /// </summary>
    public int MinimapRecognitionThreads { get; init; }
}
