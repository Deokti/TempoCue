namespace TempoCue.App.Settings;

/// <summary>Общий для UI и матча снимок применённых настроек оверлея.</summary>
public sealed class OverlaySettingsState
{
    private readonly object _sync = new();
    private OverlayUserSettings _current;

    public OverlaySettingsState(OverlayUserSettings initialSettings)
    {
        ArgumentNullException.ThrowIfNull(initialSettings);
        _current = initialSettings.Normalize();
    }

    public event EventHandler? Changed;

    public OverlayUserSettings Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public void Apply(OverlayUserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();
        lock (_sync)
        {
            if (_current == normalized)
            {
                return;
            }

            _current = normalized;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
