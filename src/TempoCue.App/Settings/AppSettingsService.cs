using System.IO;
using System.Text.Json;
using TempoCue.App.Localization;

namespace TempoCue.App.Settings;

/// <summary>Читает и сохраняет небольшой пользовательский файл настроек.</summary>
public static class AppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TempoCue");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>
    /// При первом запуске выбирает язык Windows: русский для ru, английский для остальных.
    /// Повреждённый файл настроек не должен мешать запуску приложения.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
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
    public static void SaveLanguage(string languageCode)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var settings = new AppSettings
            {
                Language = LocalizationService.NormalizeLanguageCode(languageCode)
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
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
