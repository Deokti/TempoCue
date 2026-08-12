using TempoCue.App.Settings;
using TempoCue.Overlay;

namespace TempoCue.App.Tests;

/// <summary>Управляемый сеанс предпросмотра для тестов страницы настроек.</summary>
internal sealed class TestOverlaySetupSession : IOverlaySetupSession
{
    public event EventHandler? Dismissed;

    public bool IsActive { get; private set; }

    public OverlayPresentationSettings CurrentSettings { get; private set; } = new();

    public List<OverlayUserSettings> OpenedSettings { get; } = [];

    public List<OverlayUserSettings> AppliedSettings { get; } = [];

    public int CloseCount { get; private set; }

    public void Open(OverlayUserSettings settings)
    {
        IsActive = true;
        CurrentSettings = settings.ToPresentationSettings();
        OpenedSettings.Add(settings);
    }

    public void ApplyDraft(OverlayUserSettings settings)
    {
        if (!IsActive)
        {
            return;
        }

        CurrentSettings = settings.ToPresentationSettings();
        AppliedSettings.Add(settings);
    }

    public void Close()
    {
        IsActive = false;
        CloseCount++;
    }

    public void MoveTo(double horizontal, double vertical) =>
        CurrentSettings = CurrentSettings with
        {
            HorizontalPosition = horizontal,
            VerticalPosition = vertical
        };

    public void Dismiss()
    {
        IsActive = false;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }
}
