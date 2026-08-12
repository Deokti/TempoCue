using TempoCue.Overlay;

namespace TempoCue.App.Settings;

/// <summary>Граница интерактивного предпросмотра между страницей настроек и окном оверлея.</summary>
public interface IOverlaySetupSession
{
    event EventHandler? Dismissed;

    bool IsActive { get; }

    OverlayPresentationSettings CurrentSettings { get; }

    void Open(OverlayUserSettings settings);

    void ApplyDraft(OverlayUserSettings settings);

    void Close();
}
