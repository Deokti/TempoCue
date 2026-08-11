namespace TempoCue.Overlay;

/// <summary>Внешняя граница показа и скрытия уведомлений поверх игры.</summary>
public interface IOverlayPresenter : IDisposable
{
    OverlayInteractionMode InteractionMode { get; }

    void SetInteractionMode(OverlayInteractionMode mode);

    void Show(OverlayNotification notification);

    void ShowPreview(OverlayNotification notification);

    void Hide();
}
