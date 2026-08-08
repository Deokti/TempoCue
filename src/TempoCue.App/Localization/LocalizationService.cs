using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace TempoCue.App.Localization;

/// <summary>
/// Отдаёт переводы интерфейсу и уведомляет WPF, когда пользователь меняет язык.
/// Один экземпляр сервиса используется всеми окнами приложения.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly ResourceManager ResourceManager = new(
        "TempoCue.App.Resources.Strings",
        typeof(LocalizationService).Assembly);

    private CultureInfo _culture = CultureInfo.GetCultureInfo("ru-RU");

    private LocalizationService()
    {
    }

    /// <summary>Общий экземпляр, к которому привязывается XAML.</summary>
    public static LocalizationService Instance { get; } = new();

    /// <summary>Код выбранного языка, сохраняемый в настройках.</summary>
    public string CurrentLanguageCode => _culture.Name;

    /// <summary>Возвращает перевод по стабильному ключу.</summary>
    public string this[string key] => Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Меняет язык сразу, без перезапуска приложения.</summary>
    public void SetLanguage(string? languageCode)
    {
        var normalizedCode = NormalizeLanguageCode(languageCode);
        var languageChanged = !string.Equals(
            _culture.Name,
            normalizedCode,
            StringComparison.OrdinalIgnoreCase);
        _culture = CultureInfo.GetCultureInfo(normalizedCode);

        // CurrentCulture отвечает за числа и даты, CurrentUICulture — за выбор .resx.
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _culture;
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _culture;

        if (!languageChanged)
        {
            return;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguageCode)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    /// <summary>Возвращает перевод или заметный ключ, если перевод забыли добавить.</summary>
    public string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ResourceManager.GetString(key, _culture) ?? $"[{key}]";
    }

    /// <summary>Подставляет динамические значения в локализованную строку.</summary>
    public string Format(string key, params object?[] arguments) =>
        string.Format(_culture, Get(key), arguments);

    /// <summary>
    /// Выбирает правильную форму существительного. Для русского используются
    /// формы One/Few/Many, для английского — One/Other.
    /// </summary>
    public string Plural(string keyPrefix, int count)
    {
        var suffix = _culture.TwoLetterISOLanguageName == "ru"
            ? GetRussianPluralSuffix(count)
            : count == 1 ? "One" : "Other";

        return Format($"{keyPrefix}.{suffix}", count);
    }

    /// <summary>Поддерживаются только русский и английский, остальные системы начинают с английского.</summary>
    public static string NormalizeLanguageCode(string? languageCode)
    {
        if (languageCode?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "ru-RU";
        }

        return "en-US";
    }

    private static string GetRussianPluralSuffix(int count)
    {
        var absolute = Math.Abs(count);
        var lastTwoDigits = absolute % 100;
        if (lastTwoDigits is >= 11 and <= 14)
        {
            return "Many";
        }

        return (absolute % 10) switch
        {
            1 => "One",
            2 or 3 or 4 => "Few",
            _ => "Many"
        };
    }
}
