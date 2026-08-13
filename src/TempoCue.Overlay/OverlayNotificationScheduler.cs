namespace TempoCue.Overlay;

/// <summary>
/// Детерминированно распределяет уведомления по двум слотам без зависимости от WPF и таймеров.
/// </summary>
public sealed class OverlayNotificationScheduler
{
    private readonly OverlayDisplayPolicy _policy;
    private readonly List<OverlayDisplayItem> _visible = [];
    private readonly List<PendingNotification> _pending = [];
    private readonly Dictionary<string, DateTimeOffset> _lastShownByKey =
        new(StringComparer.Ordinal);

    private long _nextSequence;
    private DateTimeOffset? _lastLearningShownAt;

    public OverlayNotificationScheduler(OverlayDisplayPolicy? policy = null)
    {
        _policy = policy ?? new OverlayDisplayPolicy();
    }

    public OverlayScheduleSnapshot Current => CreateSnapshot();

    public OverlayScheduleSnapshot Publish(
        OverlayNotification notification,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Advance(now);

        var visibleIndex = _visible.FindIndex(item =>
            string.Equals(item.Notification.Key, notification.Key, StringComparison.Ordinal));
        if (visibleIndex >= 0)
        {
            UpdateVisible(visibleIndex, notification, now);
            return CreateSnapshot();
        }

        var pendingIndex = _pending.FindIndex(item =>
            string.Equals(item.Notification.Key, notification.Key, StringComparison.Ordinal));
        if (pendingIndex >= 0)
        {
            if (_pending[pendingIndex].Notification != notification)
            {
                _pending[pendingIndex] = new PendingNotification(
                    notification,
                    now,
                    ++_nextSequence);
            }

            return CreateSnapshot();
        }

        if (WasRecentlyShown(notification.Key, now))
        {
            return CreateSnapshot();
        }

        if (notification.Tone is OverlayNotificationTone.Paused)
        {
            _visible.Clear();
            _pending.Clear();
            Show(notification, now);
            return CreateSnapshot();
        }

        if (_visible.Any(item => item.Notification.Tone is OverlayNotificationTone.Paused))
        {
            return CreateSnapshot();
        }

        if (notification.Purpose is OverlayNotificationPurpose.Operational
            && notification.Tone is OverlayNotificationTone.Danger)
        {
            _visible.Clear();
            Show(notification, now);
            return CreateSnapshot();
        }

        if (notification.Purpose is OverlayNotificationPurpose.Operational
            && notification.Tone is OverlayNotificationTone.Caution)
        {
            _visible.RemoveAll(item =>
                item.Notification.Purpose is OverlayNotificationPurpose.Learning);
        }

        var incomingPriority = _policy.GetPriority(notification);
        if (_visible.Any(item =>
                _policy.GetPriority(item.Notification) == incomingPriority))
        {
            PlaceNotification(notification, now);
            return CreateSnapshot();
        }

        if (!CanShowNow(notification, now))
        {
            Enqueue(notification, now);
            return CreateSnapshot();
        }

        PlaceNotification(notification, now);
        return CreateSnapshot();
    }

    public OverlayScheduleSnapshot Advance(DateTimeOffset now)
    {
        _visible.RemoveAll(item => item.ExpiresAt is not null && item.ExpiresAt <= now);
        _pending.RemoveAll(item => now - item.ReceivedAt >= _policy.PendingLifetime);
        RemoveExpiredHistory(now);

        while (_visible.Count < OverlayDisplayPolicy.VisibleSlotCount)
        {
            var pendingIndex = FindNextPendingIndex(now);
            if (pendingIndex < 0)
            {
                break;
            }

            var pending = _pending[pendingIndex];
            _pending.RemoveAt(pendingIndex);
            PlaceNotification(pending.Notification, now);
        }

        return CreateSnapshot();
    }

    public OverlayScheduleSnapshot Clear()
    {
        _visible.Clear();
        _pending.Clear();
        return CreateSnapshot();
    }

    public OverlayScheduleSnapshot Reset()
    {
        _visible.Clear();
        _pending.Clear();
        _lastShownByKey.Clear();
        _lastLearningShownAt = null;
        return CreateSnapshot();
    }

    private void UpdateVisible(
        int visibleIndex,
        OverlayNotification notification,
        DateTimeOffset now)
    {
        var current = _visible[visibleIndex];
        if (current.Notification == notification)
        {
            return;
        }

        _visible[visibleIndex] = current with
        {
            Notification = notification,
            ExpiresAt = GetExpiration(notification, now)
        };
        _lastShownByKey[notification.Key] = now;
        SortVisible();
    }

    private void PlaceNotification(OverlayNotification notification, DateTimeOffset now)
    {
        if (_visible.Count < OverlayDisplayPolicy.VisibleSlotCount)
        {
            Show(notification, now);
            return;
        }

        var lowest = _visible
            .Select((item, index) => (item, index))
            .OrderBy(pair => _policy.GetPriority(pair.item.Notification))
            .ThenBy(pair => pair.item.Sequence)
            .First();
        if (_policy.GetPriority(notification) <
            _policy.GetPriority(lowest.item.Notification))
        {
            Enqueue(notification, now);
            return;
        }

        _visible.RemoveAt(lowest.index);
        Show(notification, now);
    }

    private void Show(OverlayNotification notification, DateTimeOffset now)
    {
        var item = new OverlayDisplayItem(
            ++_nextSequence,
            notification,
            now,
            GetExpiration(notification, now));
        _visible.Add(item);
        _lastShownByKey[notification.Key] = now;
        if (notification.Purpose is OverlayNotificationPurpose.Learning)
        {
            _lastLearningShownAt = now;
        }

        SortVisible();
    }

    private DateTimeOffset? GetExpiration(
        OverlayNotification notification,
        DateTimeOffset now)
    {
        var lifetime = _policy.GetLifetime(notification);
        return lifetime == Timeout.InfiniteTimeSpan ? null : now + lifetime;
    }

    private bool CanShowNow(OverlayNotification notification, DateTimeOffset now)
        => _policy.BypassesLearningCadence(notification) ||
           _lastLearningShownAt is null ||
           now - _lastLearningShownAt >= _policy.LearningCadence;

    private int FindNextPendingIndex(DateTimeOffset now)
    {
        return _pending
            .Select((item, index) => (item, index))
            .Where(pair => CanShowNow(pair.item.Notification, now))
            .OrderByDescending(pair => _policy.GetPriority(pair.item.Notification))
            .ThenByDescending(pair => pair.item.Sequence)
            .Select(pair => pair.index)
            .DefaultIfEmpty(-1)
            .First();
    }

    private void Enqueue(OverlayNotification notification, DateTimeOffset now)
    {
        _pending.Add(new PendingNotification(notification, now, ++_nextSequence));
        if (_pending.Count <= OverlayDisplayPolicy.PendingCapacity)
        {
            return;
        }

        var discarded = _pending
            .OrderBy(item => _policy.GetPriority(item.Notification))
            .ThenBy(item => item.Sequence)
            .First();
        _pending.Remove(discarded);
    }

    private bool WasRecentlyShown(string key, DateTimeOffset now) =>
        _lastShownByKey.TryGetValue(key, out var shownAt) &&
        now - shownAt < _policy.DuplicateSuppressionInterval;

    private void RemoveExpiredHistory(DateTimeOffset now)
    {
        foreach (var key in _lastShownByKey
                     .Where(pair => now - pair.Value >= _policy.DuplicateSuppressionInterval)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _lastShownByKey.Remove(key);
        }
    }

    private void SortVisible() => _visible.Sort((left, right) =>
    {
        var byPriority = _policy.GetPriority(right.Notification)
            .CompareTo(_policy.GetPriority(left.Notification));
        return byPriority != 0 ? byPriority : left.Sequence.CompareTo(right.Sequence);
    });

    private OverlayScheduleSnapshot CreateSnapshot() =>
        new(_visible.ToArray(), _pending.Count > 0);

    private sealed record PendingNotification(
        OverlayNotification Notification,
        DateTimeOffset ReceivedAt,
        long Sequence);
}
