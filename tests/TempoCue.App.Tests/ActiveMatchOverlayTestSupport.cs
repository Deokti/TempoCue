using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Overlay;

namespace TempoCue.App.Tests;

/// <summary>Общие снимки матча и тестовые границы для сценариев игрового оверлея.</summary>
internal static class ActiveMatchOverlayTestData
{
    public static ActiveMatchUpdate CreateSuccessUpdate(
        CoachPack? pack,
        CoachAdvice? advice,
        double gameTimeSeconds,
        int level,
        ActiveMapControlPlan? mapControlPlan = null)
    {
        var match = new MatchState
        {
            GameTimeSeconds = gameTimeSeconds,
            ActivePlayerId = "active-player",
            Players =
            [
                new PlayerState
                {
                    ParticipantId = "active-player",
                    ChampionName = "Urgot",
                    Team = TeamSide.Order,
                    Position = PlayerPosition.Top,
                    Level = level
                },
                new PlayerState
                {
                    ParticipantId = "opponent",
                    ChampionName = "Sett",
                    Team = TeamSide.Chaos,
                    Position = PlayerPosition.Top,
                    Level = level
                }
            ]
        };
        var snapshot = new MatchSnapshot(match, [], DateTimeOffset.UtcNow);
        return new ActiveMatchUpdate(
            MatchSnapshotReadStatus.Success,
            snapshot,
            pack is not null && advice is not null
                ? ActiveCoachPackSelection.Selected(pack, advice)
                : ActiveCoachPackSelection.NotFound(),
            mapControlPlan);
    }

    public static CoachAdvice CreateAdvice(
        string ruleId,
        PowerState powerState,
        string title) => new(
            ruleId,
            title,
            "Содержимое совета",
            powerState,
            1,
            "Тестовый источник",
            [],
            [],
            string.Empty,
            string.Empty,
            []);

    public static CoachPersistentTipDefinition CreatePersistentTip(
        string id,
        string title,
        string? applicableRuleId = null) => new()
        {
            Id = id,
            Title = title,
            Message = $"Текст подсказки {id}",
            ApplicableRuleId = applicableRuleId
        };
}

internal sealed class TestActiveMatchMonitor : IActiveMatchMonitor
{
    public event EventHandler<ActiveMatchUpdateEventArgs>? Updated;

    public ActiveMatchUpdate? CurrentUpdate { get; private set; }

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void Publish(ActiveMatchUpdate update)
    {
        CurrentUpdate = update;
        Updated?.Invoke(this, new ActiveMatchUpdateEventArgs(update));
    }
}

internal sealed class RecordingOverlayPresenter : IOverlayPresenter
{
    public event EventHandler? SetupDismissed;

    public List<OverlayNotification> Notifications { get; } = [];

    public OverlayInteractionMode InteractionMode { get; private set; } =
        OverlayInteractionMode.Setup;

    public OverlayPresentationSettings Settings { get; private set; } = new();

    public void ApplySettings(OverlayPresentationSettings settings) => Settings = settings;

    public void SetInteractionMode(OverlayInteractionMode mode) => InteractionMode = mode;

    public void Show(OverlayNotification notification) => Notifications.Add(notification);

    public void ShowPreview(IReadOnlyList<OverlayNotification> notifications) =>
        Notifications.AddRange(notifications);

    public void Hide()
    {
    }

    public void Dispose()
    {
    }

    public void DismissSetup() => SetupDismissed?.Invoke(this, EventArgs.Empty);
}
