using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.Overlay;

namespace TempoCue.App.ViewModels;

/// <summary>Состояние страницы настроек и сохранение выбранного языка.</summary>
public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private readonly AppSettingsService _settings;
    private readonly OverlaySettingsState _overlaySettings;
    private readonly Action _showOverlayPreview;
    private readonly IOverlaySetupSession? _overlaySetupSession;
    private string _selectedLanguageCode;
    private bool _isOverlayEnabled;
    private int _overlayWidth;
    private int _backgroundOpacityPercent;
    private OverlayDurationMode _durationMode;
    private int _fixedDurationSeconds;
    private PersistentAdviceFrequency _persistentAdviceFrequency;
    private double? _horizontalPosition;
    private double? _verticalPosition;
    private OverlayUserSettings _savedOverlaySettings;
    private bool _overlaySetupWasOpened;
    private bool _isApplyingDraft;
    private bool _isDisposed;

    public SettingsViewModel(
        LocalizationService localization,
        AppSettingsService settings,
        Action showOverlayPreview,
        OverlaySettingsState? overlaySettings = null,
        IOverlaySetupSession? overlaySetupSession = null,
        CaptureDiagnosticsViewModel? captureDiagnostics = null)
    {
        _localization = localization;
        _settings = settings;
        _showOverlayPreview = showOverlayPreview ?? throw new ArgumentNullException(nameof(showOverlayPreview));
        _overlaySettings = overlaySettings ?? new OverlaySettingsState(settings.Load().Overlay);
        _overlaySetupSession = overlaySetupSession;
        CaptureDiagnostics = captureDiagnostics;
        _selectedLanguageCode = localization.CurrentLanguageCode;
        var overlay = _overlaySettings.Current;
        _isOverlayEnabled = overlay.IsEnabled;
        _overlayWidth = overlay.Width;
        _backgroundOpacityPercent = overlay.BackgroundOpacityPercent;
        _durationMode = overlay.DurationMode;
        _fixedDurationSeconds = overlay.FixedDurationSeconds;
        _persistentAdviceFrequency = overlay.PersistentAdviceFrequency;
        _horizontalPosition = overlay.HorizontalPosition;
        _verticalPosition = overlay.VerticalPosition;
        _savedOverlaySettings = overlay;
        _localization.PropertyChanged += Localization_PropertyChanged;
        if (_overlaySetupSession is not null)
        {
            _overlaySetupSession.Dismissed += OverlaySetupSession_Dismissed;
        }
    }

    public IReadOnlyList<LanguageOptionViewModel> Languages { get; } =
    [
        new("ru-RU", "Русский"),
        new("en-US", "English")
    ];

    public IReadOnlyList<int> OverlayWidths { get; } = [300, 340, 400];

    public CaptureDiagnosticsViewModel? CaptureDiagnostics { get; }

    public bool IsOverlayEnabled
    {
        get => _isOverlayEnabled;
        set
        {
            if (SetProperty(ref _isOverlayEnabled, value))
            {
                NotifyOverlayDraftChanged();
            }
        }
    }

    public int OverlayWidth
    {
        get => _overlayWidth;
        set
        {
            if (SetProperty(ref _overlayWidth, Math.Clamp(value, 300, 400)))
            {
                NotifyOverlayDraftChanged();
            }
        }
    }

    public int BackgroundOpacityPercent
    {
        get => _backgroundOpacityPercent;
        set
        {
            if (SetProperty(ref _backgroundOpacityPercent, Math.Clamp(value, 60, 100)))
            {
                NotifyOverlayDraftChanged();
            }
        }
    }

    public int FixedDurationSeconds
    {
        get => _fixedDurationSeconds;
        set
        {
            if (SetProperty(ref _fixedDurationSeconds, Math.Clamp(value, 5, 20)))
            {
                NotifyOverlayDraftChanged();
            }
        }
    }

    public bool IsOverlaySettingsDirty => BuildOverlaySettings().Normalize() != _savedOverlaySettings;

    public bool IsAutomaticDuration
    {
        get => _durationMode is OverlayDurationMode.Automatic;
        set
        {
            if (value)
            {
                SetDurationMode(OverlayDurationMode.Automatic);
            }
        }
    }

    public bool IsFixedDuration
    {
        get => _durationMode is OverlayDurationMode.Fixed;
        set
        {
            if (value)
            {
                SetDurationMode(OverlayDurationMode.Fixed);
            }
        }
    }

    public bool IsPersistentAdviceOnce
    {
        get => _persistentAdviceFrequency is PersistentAdviceFrequency.OncePerMatch;
        set
        {
            if (value)
            {
                SetPersistentAdviceFrequency(PersistentAdviceFrequency.OncePerMatch);
            }
        }
    }

    public bool IsPersistentAdviceEveryLevel
    {
        get => _persistentAdviceFrequency is PersistentAdviceFrequency.EveryLevel;
        set
        {
            if (value)
            {
                SetPersistentAdviceFrequency(PersistentAdviceFrequency.EveryLevel);
            }
        }
    }

    public bool IsPersistentAdviceDisabled
    {
        get => _persistentAdviceFrequency is PersistentAdviceFrequency.Disabled;
        set
        {
            if (value)
            {
                SetPersistentAdviceFrequency(PersistentAdviceFrequency.Disabled);
            }
        }
    }

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

    /// <summary>Показывает демонстрационное уведомление без активного матча.</summary>
    public void ShowOverlayPreview() => _showOverlayPreview();

    /// <summary>Открывает интерактивный предпросмотр даже для отключённого игрового оверлея.</summary>
    public void OpenOverlaySetup()
    {
        if (_overlaySetupSession is null)
        {
            _showOverlayPreview();
            return;
        }

        CaptureCurrentSetupPlacement();
        _overlaySetupWasOpened = true;
        _overlaySetupSession.Open(BuildOverlaySettings());
    }

    /// <summary>Закрывает предпросмотр и возвращает игровому оверлею сохранённые настройки.</summary>
    public void CloseOverlaySetup()
    {
        if (!IsOverlaySetupActive())
        {
            _overlaySetupWasOpened = false;
            return;
        }

        _overlaySetupWasOpened = false;
        _overlaySetupSession?.Close();
    }

    public void SaveOverlaySettings()
    {
        var settings = BuildOverlaySettings();
        if (IsOverlaySetupActive())
        {
            settings = settings.WithPlacement(_overlaySetupSession!.CurrentSettings);
        }

        settings = settings.Normalize();
        _settings.SaveOverlay(settings);
        _overlaySettings.Apply(settings);
        ApplyDraft(settings);
        _savedOverlaySettings = settings;
        NotifyOverlayDraftChanged();
    }

    public void ResetOverlaySettings()
    {
        ApplyDraft(new OverlayUserSettings());
        if (IsOverlaySetupActive())
        {
            _overlaySetupSession!.ApplyDraft(BuildOverlaySettings());
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        CaptureDiagnostics?.Dispose();
        _localization.PropertyChanged -= Localization_PropertyChanged;
        if (_overlaySetupSession is not null)
        {
            _overlaySetupSession.Dismissed -= OverlaySetupSession_Dismissed;
        }
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(LocalizationService.CurrentLanguageCode))
        {
            return;
        }

        SetProperty(ref _selectedLanguageCode, _localization.CurrentLanguageCode, nameof(SelectedLanguageCode));
    }

    private OverlayUserSettings BuildOverlaySettings() => new()
    {
        IsEnabled = IsOverlayEnabled,
        Width = OverlayWidth,
        BackgroundOpacityPercent = BackgroundOpacityPercent,
        DurationMode = _durationMode,
        FixedDurationSeconds = FixedDurationSeconds,
        PersistentAdviceFrequency = _persistentAdviceFrequency,
        HorizontalPosition = _horizontalPosition,
        VerticalPosition = _verticalPosition
    };

    private void ApplyDraft(OverlayUserSettings settings)
    {
        _isApplyingDraft = true;
        try
        {
            IsOverlayEnabled = settings.IsEnabled;
            OverlayWidth = settings.Width;
            BackgroundOpacityPercent = settings.BackgroundOpacityPercent;
            FixedDurationSeconds = settings.FixedDurationSeconds;
            _horizontalPosition = settings.HorizontalPosition;
            _verticalPosition = settings.VerticalPosition;
            SetDurationMode(settings.DurationMode);
            SetPersistentAdviceFrequency(settings.PersistentAdviceFrequency);
        }
        finally
        {
            _isApplyingDraft = false;
        }

        OnPropertyChanged(nameof(IsOverlaySettingsDirty));
    }

    private void SetDurationMode(OverlayDurationMode mode)
    {
        if (_durationMode == mode)
        {
            return;
        }

        _durationMode = mode;
        OnPropertyChanged(nameof(IsAutomaticDuration));
        OnPropertyChanged(nameof(IsFixedDuration));
        NotifyOverlayDraftChanged();
    }

    private void SetPersistentAdviceFrequency(PersistentAdviceFrequency frequency)
    {
        if (_persistentAdviceFrequency == frequency)
        {
            return;
        }

        _persistentAdviceFrequency = frequency;
        OnPropertyChanged(nameof(IsPersistentAdviceOnce));
        OnPropertyChanged(nameof(IsPersistentAdviceEveryLevel));
        OnPropertyChanged(nameof(IsPersistentAdviceDisabled));
        NotifyOverlayDraftChanged();
    }

    private void NotifyOverlayDraftChanged()
    {
        OnPropertyChanged(nameof(IsOverlaySettingsDirty));
        if (_isApplyingDraft || !IsOverlaySetupActive())
        {
            return;
        }

        CaptureCurrentSetupPlacement();
        _overlaySetupSession!.ApplyDraft(BuildOverlaySettings());
    }

    private bool IsOverlaySetupActive()
    {
        if (!_overlaySetupWasOpened)
        {
            return false;
        }

        if (_overlaySetupSession?.IsActive is true)
        {
            return true;
        }

        _overlaySetupWasOpened = false;
        return false;
    }

    private void CaptureCurrentSetupPlacement()
    {
        if (!IsOverlaySetupActive())
        {
            return;
        }

        var presentation = _overlaySetupSession!.CurrentSettings;
        _horizontalPosition = presentation.HorizontalPosition;
        _verticalPosition = presentation.VerticalPosition;
    }

    private void OverlaySetupSession_Dismissed(object? sender, EventArgs e) =>
        _overlaySetupWasOpened = false;
}
