using TempoCue.App.Localization;
using TempoCue.Overlay;

namespace TempoCue.App.Settings;

/// <summary>Владеет жизненным циклом безопасного предпросмотра и возвратом игрового runtime.</summary>
public sealed class OverlaySetupSession : IOverlaySetupSession, IDisposable
{
    private readonly IOverlayPresenter _presenter;
    private readonly OverlaySettingsState _savedSettings;
    private readonly LocalizationService _localization;
    private bool _isDisposed;

    public OverlaySetupSession(
        IOverlayPresenter presenter,
        OverlaySettingsState savedSettings,
        LocalizationService localization)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _savedSettings = savedSettings ?? throw new ArgumentNullException(nameof(savedSettings));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _presenter.SetupDismissed += Presenter_SetupDismissed;
    }

    public event EventHandler? Dismissed;

    public bool IsActive => !_isDisposed &&
        _presenter.InteractionMode is OverlayInteractionMode.Setup;

    public OverlayPresentationSettings CurrentSettings => _presenter.Settings;

    public void Open(OverlayUserSettings settings)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        _presenter.ApplySettings(settings.ToPresentationSettings());
        _presenter.SetInteractionMode(OverlayInteractionMode.Setup);
        _presenter.ShowPreview(
        [
            OverlayPreviewNotificationFactory.Create(
                _localization,
                "Warning",
                OverlayNotificationTone.Caution),
            OverlayPreviewNotificationFactory.Create(
                _localization,
                "Information",
                OverlayNotificationTone.Information)
        ]);
    }

    public void ApplyDraft(OverlayUserSettings settings)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        if (IsActive)
        {
            _presenter.ApplySettings(settings.ToPresentationSettings());
        }
    }

    public void Close()
    {
        if (_isDisposed)
        {
            return;
        }

        if (IsActive)
        {
            _presenter.Hide();
        }

        RestoreSavedSettings();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _presenter.SetupDismissed -= Presenter_SetupDismissed;
    }

    private void Presenter_SetupDismissed(object? sender, EventArgs e)
    {
        RestoreSavedSettings();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreSavedSettings()
    {
        _presenter.SetInteractionMode(OverlayInteractionMode.Game);
        _presenter.ApplySettings(_savedSettings.Current.ToPresentationSettings());
    }
}
