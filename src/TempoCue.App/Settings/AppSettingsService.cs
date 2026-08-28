using System.IO;
using System.Text.Json;
using TempoCue.App.Localization;

namespace TempoCue.App.Settings;

/// <summary>Читает и сохраняет небольшой пользовательский файл настроек.</summary>
public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public AppSettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TempoCue",
            "settings.json"))
    {
    }

    public AppSettingsService(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
    }

    /// <summary>
    /// При первом запуске выбирает язык Windows: русский для ru, английский для остальных.
    /// Повреждённый файл настроек не должен мешать запуску приложения.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
                if (settings is not null)
                {
                    return settings with
                    {
                        Language = LocalizationService.NormalizeLanguageCode(settings.Language),
                        Overlay = (settings.Overlay ?? new OverlayUserSettings()).Normalize(),
                        // Отрицательное число потоков в подправленном вручную файле
                        // не должно ронять запуск: оно означает «выбрать по машине».
                        MinimapRecognitionThreads =
                            Math.Max(0, settings.MinimapRecognitionThreads),
                        MinimapRecognizer = Migrate(settings)
                    };
                }
            }
        }
        catch (IOException)
        {
            // Используем безопасное значение по умолчанию.
        }
        catch (JsonException)
        {
            // Используем безопасное значение по умолчанию.
        }
        catch (UnauthorizedAccessException)
        {
            // Приложение продолжит работать, даже если настройки нельзя прочитать.
        }

        return new AppSettings
        {
            Language = LocalizationService.NormalizeLanguageCode(
                System.Globalization.CultureInfo.CurrentUICulture.Name)
        };
    }

    /// <summary>Сохраняет язык сразу после выбора в интерфейсе.</summary>
    public void SaveLanguage(string languageCode)
    {
        var current = Load();
        Save(current with
        {
            Language = LocalizationService.NormalizeLanguageCode(languageCode)
        });
    }

    /// <summary>Сохраняет применённые параметры оверлея, не изменяя язык.</summary>
    public void SaveOverlay(OverlayUserSettings overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        var current = Load();
        Save(current with { Overlay = overlay.Normalize() });
    }

    /// <summary>
    /// Приводит сохранённый выбор к существующему варианту.
    /// </summary>
    /// <remarks>
    /// Выбор хранится числом, и System.Text.Json не проверяет, есть ли такое
    /// значение в перечислении. В файлах уже лежит двойка — удалённая сторонняя
    /// модель DeeperLeague, — и её надо прочитать как «выбора нет», то есть
    /// применить умолчание приложения. Прежняя галочка «экспериментальное
    /// распознавание» указывала на ту же удалённую модель и теперь не значит
    /// ничего: поле остаётся в старых файлах и просто не читается.
    /// </remarks>
    private static MinimapRecognizerChoice? Migrate(AppSettings settings) =>
        settings.MinimapRecognizer is { } chosen && Enum.IsDefined(chosen)
            ? chosen
            : null;

    /// <summary>
    /// Сохраняет выбор распознавателя миникарты. Он применяется при следующем
    /// запуске: способ распознавания выбирается один раз при создании сессии
    /// захвата, и подмена на ходу усложнила бы её жизненный цикл без пользы.
    /// </summary>
    public void SaveMinimapRecognizer(MinimapRecognizerChoice choice)
    {
        if (!Enum.IsDefined(choice))
        {
            throw new ArgumentOutOfRangeException(nameof(choice));
        }

        var current = Load();
        Save(current with { MinimapRecognizer = choice });
    }

    /// <summary>
    /// Сохраняет число потоков распознавания. Применяется при следующем запуске по
    /// той же причине, что и выбор распознавателя: он создаётся один раз вместе с
    /// сессией захвата.
    /// </summary>
    public void SaveMinimapRecognitionThreads(int threads)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threads);
        var current = Load();
        Save(current with { MinimapRecognitionThreads = threads });
    }

    private void Save(AppSettings settings)
    {
        try
        {
            var settingsDirectory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
        }
        catch (IOException)
        {
            // Ошибка сохранения не должна прерывать матч или закрывать приложение.
        }
        catch (UnauthorizedAccessException)
        {
            // Текущий выбор продолжит работать до закрытия приложения.
        }
    }
}
