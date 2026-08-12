namespace TempoCue.Overlay;

/// <summary>Единые интервалы и приоритеты жизненного цикла карточек оверлея.</summary>
public sealed class OverlayDisplayPolicy
{
    private const double ReadingCharactersPerSecond = 18;
    private const double ReadingPreparationSeconds = 2.5;
    private const double MaximumReadingLifetimeSeconds = 16;

    public const int VisibleSlotCount = 2;
    public const int PendingCapacity = 2;

    public OverlayPresentationSettings PresentationSettings { get; private set; } = new();

    public void ApplySettings(OverlayPresentationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        PresentationSettings = settings.Normalize();
    }

    public TimeSpan DuplicateSuppressionInterval { get; init; } = TimeSpan.FromSeconds(20);

    public TimeSpan LowPriorityCadence { get; init; } = TimeSpan.FromSeconds(8);

    public TimeSpan PendingLifetime { get; init; } = TimeSpan.FromSeconds(20);

    public TimeSpan EnterDuration { get; init; } = TimeSpan.FromMilliseconds(180);

    public TimeSpan UrgentEnterDuration { get; init; } = TimeSpan.FromMilliseconds(120);

    public TimeSpan ExitDuration { get; init; } = TimeSpan.FromMilliseconds(150);

    public TimeSpan GetLifetime(OverlayNotificationTone tone) => tone switch
    {
        OverlayNotificationTone.Danger => TimeSpan.FromSeconds(8),
        OverlayNotificationTone.Caution => TimeSpan.FromSeconds(6),
        OverlayNotificationTone.Information => TimeSpan.FromSeconds(5),
        OverlayNotificationTone.Coach => TimeSpan.FromSeconds(7),
        OverlayNotificationTone.NoData => TimeSpan.FromSeconds(5),
        OverlayNotificationTone.Paused => Timeout.InfiniteTimeSpan,
        _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, null)
    };

    public TimeSpan GetLifetime(OverlayNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var minimumLifetime = GetLifetime(notification.Tone);
        if (minimumLifetime == Timeout.InfiniteTimeSpan)
        {
            return minimumLifetime;
        }

        if (PresentationSettings.DurationMode is OverlayDurationMode.Fixed)
        {
            var fixedSeconds = PresentationSettings.FixedDurationSeconds;
            if (notification.Tone is OverlayNotificationTone.Danger)
            {
                fixedSeconds = Math.Max(fixedSeconds, 6);
            }

            return TimeSpan.FromSeconds(fixedSeconds);
        }

        var readableCharacterCount = notification.Phrase.Length + notification.Source.Length;
        var readingSeconds = Math.Ceiling(
            ReadingPreparationSeconds + readableCharacterCount / ReadingCharactersPerSecond);
        var lifetimeSeconds = Math.Clamp(
            readingSeconds,
            minimumLifetime.TotalSeconds,
            MaximumReadingLifetimeSeconds);
        return TimeSpan.FromSeconds(lifetimeSeconds);
    }

    public int GetPriority(OverlayNotificationTone tone) => tone switch
    {
        OverlayNotificationTone.Danger => 5,
        OverlayNotificationTone.Caution => 4,
        OverlayNotificationTone.Information => 3,
        OverlayNotificationTone.Coach => 2,
        OverlayNotificationTone.NoData => 1,
        OverlayNotificationTone.Paused => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, null)
    };

    public bool IsWarning(OverlayNotificationTone tone) =>
        tone is OverlayNotificationTone.Danger or OverlayNotificationTone.Caution;

    public bool BypassesCadence(OverlayNotificationTone tone) => IsWarning(tone) ||
        tone is OverlayNotificationTone.Paused;
}
