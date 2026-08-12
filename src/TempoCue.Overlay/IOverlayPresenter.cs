namespace TempoCue.Overlay;

/// <summary>Внешняя граница показа и скрытия уведомлений поверх игры.</summary>
public interface IOverlayPresenter : IDisposable
{
    event EventHandler? SetupDismissed;

    OverlayInteractionMode InteractionMode { get; }

    OverlayPresentationSettings Settings { get; }

    void ApplySettings(OverlayPresentationSettings settings);

    void SetInteractionMode(OverlayInteractionMode mode);

    void Show(OverlayNotification notification);

    void ShowPreview(IReadOnlyList<OverlayNotification> notifications);

    void Hide();
}
