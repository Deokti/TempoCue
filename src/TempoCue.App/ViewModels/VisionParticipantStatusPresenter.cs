using System.Globalization;
using System.Windows.Media;
using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Vision;

namespace TempoCue.App.ViewModels;

/// <summary>Форматирует диагностическое состояние Vision для двух колонок интерфейса.</summary>
internal sealed class VisionParticipantStatusPresenter(LocalizationService localization)
{
    /// <summary>
    /// Когда участника видели в последний раз.
    /// </summary>
    /// <remarks>
    /// Игровое время, а не часы: игрок считает по нему возрождение и подход
    /// соперника, и на паузе одно из другого не пересчитать. Часы остаются
    /// запасным вариантом — до первого ответа Live Client Data игрового времени
    /// ещё нет.
    /// </remarks>
    private string? LastSeenLabel(ActiveMatchVisionParticipantSnapshot participant) =>
        participant.LastSeenGameTime is { } gameTime
            ? string.Format(
                CultureInfo.InvariantCulture,
                "{0:mm\\:ss}",
                gameTime)
            : participant.LastSeenAt is { } wallClock
                ? wallClock.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture)
                : null;

    private static readonly Brush VisibleStatusBrush = CreateFrozenBrush(87, 221, 179);
    private static readonly Brush MissingStatusBrush = CreateFrozenBrush(119, 139, 167);
    private static readonly Brush PendingStatusBrush = CreateFrozenBrush(255, 201, 85);
    private static readonly Brush UnavailableStatusBrush = CreateFrozenBrush(255, 89, 116);
    private readonly LocalizationService _localization =
        localization ?? throw new ArgumentNullException(nameof(localization));

    public IReadOnlyList<VisionParticipantStatusViewModel> Create(
        ActiveMatchVisionDiagnosticsSnapshot snapshot,
        MinimapMarkerAffiliation affiliation) =>
        snapshot.Participants
            .Where(participant => participant.RelativeAffiliation == affiliation)
            .OrderBy(static participant => GetPositionOrder(participant.Position))
            .ThenBy(static participant => participant.ChampionName, StringComparer.OrdinalIgnoreCase)
            .Select(CreateParticipantStatus)
            .ToArray();

    private VisionParticipantStatusViewModel CreateParticipantStatus(
        ActiveMatchVisionParticipantSnapshot participant)
    {
        var relativeTeam = _localization.Get(
            participant.RelativeAffiliation == MinimapMarkerAffiliation.Ally
                ? "Settings.Capture.Participants.RelativeTeam.Ally"
                : "Settings.Capture.Participants.RelativeTeam.Enemy");
        var teamTrace = string.Format(
            CultureInfo.CurrentCulture,
            _localization.Get("Settings.Capture.Participants.TeamTrace.WithRing.Value"),
            FormatRawTeam(participant.RawTeam),
            relativeTeam,
            "—");

        var (statusText, statusBrush) = CreateStatus(participant);
        return new VisionParticipantStatusViewModel(
            participant.ChampionName,
            FormatPositionCode(participant.Position),
            statusText,
            teamTrace,
            statusBrush);
    }

    private (string Text, Brush Brush) CreateStatus(
        ActiveMatchVisionParticipantSnapshot participant)
    {
        if (participant.IsDead)
        {
            return (
                _localization.Get("Settings.Capture.Participants.Status.Dead"),
                MissingStatusBrush);
        }

        if (participant.ReferenceStatus == ChampionPortraitReferenceStatus.Pending)
        {
            return (
                _localization.Get("Settings.Capture.Participants.Status.ReferencePending"),
                PendingStatusBrush);
        }

        if (participant.ReferenceStatus == ChampionPortraitReferenceStatus.Unavailable)
        {
            return (
                _localization.Get("Settings.Capture.Participants.Status.ReferenceUnavailable"),
                UnavailableStatusBrush);
        }

        if (participant.VisibilityStatus == ParticipantVisibilityStatus.Visible)
        {
            return (
                string.Format(
                    CultureInfo.CurrentCulture,
                    _localization.Get("Settings.Capture.Participants.Status.Visible.Value"),
                    participant.VisibilityConfidence),
                VisibleStatusBrush);
        }

        if (participant.VisibilityStatus == ParticipantVisibilityStatus.Confirming)
        {
            return (
                string.Format(
                    CultureInfo.CurrentCulture,
                    _localization.Get("Settings.Capture.Participants.Status.Confirming.Value"),
                    participant.VisibilityConfidence),
                PendingStatusBrush);
        }

        if (participant.VisibilityStatus == ParticipantVisibilityStatus.Unknown)
        {
            // Потеря потока наблюдений не должна выглядеть как подтверждённое исчезновение.
            return (
                LastSeenLabel(participant) is { } lastConfirmed
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        _localization.Get("Settings.Capture.Participants.Status.NoData.LastSeen.Value"),
                        lastConfirmed)
                    : _localization.Get("Settings.Capture.Participants.Status.NoData"),
                MissingStatusBrush);
        }

        return LastSeenLabel(participant) is { } lastSeen
            ? (
                string.Format(
                    CultureInfo.CurrentCulture,
                    _localization.Get("Settings.Capture.Participants.Status.LastSeen.Value"),
                    lastSeen),
                MissingStatusBrush)
            : (
                _localization.Get("Settings.Capture.Participants.Status.NotVisible"),
                MissingStatusBrush);
    }

    public string FormatRawTeam(TeamSide team) => team switch
    {
        TeamSide.Order => "ORDER",
        TeamSide.Chaos => "CHAOS",
        _ => "UNKNOWN"
    };

    private static string FormatPositionCode(PlayerPosition position) => position switch
    {
        PlayerPosition.Top => "TOP",
        PlayerPosition.Jungle => "JGL",
        PlayerPosition.Middle => "MID",
        PlayerPosition.Bottom => "BOT",
        PlayerPosition.Support => "SUP",
        _ => "—"
    };

    private static int GetPositionOrder(PlayerPosition position) => position switch
    {
        PlayerPosition.Top => 0,
        PlayerPosition.Jungle => 1,
        PlayerPosition.Middle => 2,
        PlayerPosition.Bottom => 3,
        PlayerPosition.Support => 4,
        _ => 5
    };

    private static Brush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
