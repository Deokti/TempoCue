using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.App.Settings;

namespace TempoCue.App.ViewModels;

/// <summary>Состояние страницы настроек и сохранение выбранного языка.</summary>
public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private readonly AppSettingsService _settings;
    private string _selectedLanguageCode;
    private bool _isDisposed;

    public SettingsViewModel(
        LocalizationService localization,
        AppSettingsService settings)
    {
        _localization = localization;
        _settings = settings;
        _selectedLanguageCode = localization.CurrentLanguageCode;
        _localization.PropertyChanged += Localization_PropertyChanged;
    }

    public IReadOnlyList<LanguageOptionViewModel> Languages { get; } =
    [
        new("ru-RU", "Русский"),
        new("en-US", "English")
    ];

    public string SelectedLanguageCode
    {
        get => _selectedLanguageCode;
        set
        {
            var normalizedCode = LocalizationService.NormalizeLanguageCode(value);
            if (!SetProperty(ref _selectedLanguageCode, normalizedCode))
            {
                return;
            }

            _localization.SetLanguage(normalizedCode);
            _settings.SaveLanguage(normalizedCode);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= Localization_PropertyChanged;
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(LocalizationService.CurrentLanguageCode))
        {
            return;
        }

        SetProperty(ref _selectedLanguageCode, _localization.CurrentLanguageCode, nameof(SelectedLanguageCode));
    }
}
