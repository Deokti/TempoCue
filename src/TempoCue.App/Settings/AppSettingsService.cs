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
                        Language = LocalizationService.NormalizeLanguageCode(settings.Language)
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
        try
        {
            var settingsDirectory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            var settings = new AppSettings
            {
                Language = LocalizationService.NormalizeLanguageCode(languageCode)
            };
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
