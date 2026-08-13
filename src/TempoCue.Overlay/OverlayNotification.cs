namespace TempoCue.Overlay;

/// <summary>Готовое к показу сообщение, не содержащее WPF-типов и игрового состояния.</summary>
public sealed record OverlayNotification
{
    public OverlayNotification(
        string key,
        string status,
        string phrase,
        string source,
        string dismissLabel,
        OverlayNotificationTone tone = OverlayNotificationTone.Information,
        string? context = null,
        string? badge = null,
        OverlayNotificationPurpose purpose = OverlayNotificationPurpose.Operational)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(phrase);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(dismissLabel);

        Key = key.Trim();
        Status = status.Trim();
        Phrase = phrase.Trim();
        Source = source.Trim();
        DismissLabel = dismissLabel.Trim();
        Tone = tone;
        Purpose = purpose;
        Context = NormalizeOptional(context);
        Badge = NormalizeOptional(badge);
    }

    /// <summary>Стабильный ключ события для обновления на месте и подавления повторов.</summary>
    public string Key { get; }

    public string Status { get; }

    public string DisplayStatus => Status.ToUpperInvariant();

    public string Phrase { get; }

    public string Source { get; }

    public string DismissLabel { get; }

    public OverlayNotificationTone Tone { get; }

    /// <summary>Роль сообщения при распределении ограниченного внимания и слотов оверлея.</summary>
    public OverlayNotificationPurpose Purpose { get; }

    public string? Context { get; }

    public string? Badge { get; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
