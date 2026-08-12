using TempoCue.App.Localization;
using TempoCue.Overlay;

namespace TempoCue.App.Settings;

/// <summary>Создаёт локализованные уведомления для проверки оверлея без матча.</summary>
internal static class OverlayPreviewNotificationFactory
{
    public static OverlayNotification Create(
        LocalizationService localization,
        string resourceSuffix,
        OverlayNotificationTone tone) => new(
        $"settings.preview.{resourceSuffix.ToLowerInvariant()}",
        localization.Get($"Settings.Overlay.Preview.{resourceSuffix}.Label"),
        localization.Get($"Settings.Overlay.Preview.{resourceSuffix}.Title"),
        localization.Get($"Settings.Overlay.Preview.{resourceSuffix}.Message"),
        localization.Get("Settings.Overlay.Preview.Close"),
        tone);
}
