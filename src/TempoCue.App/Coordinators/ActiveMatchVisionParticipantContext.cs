using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Vision;

namespace TempoCue.App.Coordinators;

/// <summary>Стабильный состав матча, необходимый для подготовки и диагностики Vision.</summary>
internal sealed record ActiveMatchVisionParticipantContext(
    string Signature,
    string LanguageCode,
    TeamSide ActivePlayerTeam,
    IReadOnlyList<ActiveMatchVisionParticipant> Players)
{
    public static ActiveMatchVisionParticipantContext? Create(
        ActiveMatchUpdate? update,
        string languageCode)
    {
        if (update is not
            {
                Status: MatchSnapshotReadStatus.Success,
                Snapshot.Match: { } match
            })
        {
            return null;
        }

        var activePlayer = match.GetActivePlayer();
        if (activePlayer is null || activePlayer.Team == TeamSide.Unknown)
        {
            return null;
        }

        var players = match.Players
            .Where(static player =>
                player.Team != TeamSide.Unknown &&
                !string.IsNullOrWhiteSpace(player.ChampionName))
            .Select(player => new ActiveMatchVisionParticipant(
                player.ParticipantId,
                player.ChampionName,
                player.Position,
                player.Team,
                player.IsDead,
                player.Team == activePlayer.Team
                    ? MinimapMarkerAffiliation.Ally
                    : MinimapMarkerAffiliation.Enemy))
            .ToArray();
        if (players.Length == 0)
        {
            return null;
        }

        var rosterSignature = string.Join(
            '|',
            players
                .Select(static player => player.ReferenceKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase));
        return new ActiveMatchVisionParticipantContext(
            $"{languageCode}|{activePlayer.Team}|{rosterSignature}",
            languageCode,
            activePlayer.Team,
            players);
    }

    public ActiveMatchVisionDiagnosticsSnapshot CreateDiagnosticsSnapshot(
        IReadOnlyDictionary<string, ActiveMatchVisionReferenceResolution>? resolutions = null,
        IReadOnlyDictionary<string, ParticipantVisibilitySnapshot>? visibilityStates = null,
        bool observationsAreStale = false) =>
        new(
            ActivePlayerTeam,
            Players.Select(player =>
            {
                var resolution = resolutions is not null && resolutions.TryGetValue(
                    player.ReferenceKey,
                    out var resolved)
                        ? resolved
                        : null;
                var visibility = visibilityStates is not null &&
                    visibilityStates.TryGetValue(player.ParticipantId, out var tracked)
                        ? tracked
                        : null;
                // Без живого потока «не виден» был бы догадкой, поэтому сохраняем
                // последнее подтверждение, но честно показываем отсутствие данных.
                var visibilityStatus = observationsAreStale || visibility is null
                    ? ParticipantVisibilityStatus.Unknown
                    : visibility.Status;
                return new ActiveMatchVisionParticipantSnapshot(
                    player.ParticipantId,
                    player.ChampionName,
                    resolution?.ChampionId,
                    player.Position,
                    player.RawTeam,
                    player.Affiliation,
                    resolution?.Status ?? ChampionPortraitReferenceStatus.Pending,
                    player.IsDead,
                    visibilityStatus,
                    visibility?.LastSeenAt,
                    visibility?.Confidence ?? 0,
                    visibility?.MapX,
                    visibility?.MapY);
            }).ToArray());
}

internal sealed record ActiveMatchVisionParticipant(
    string ParticipantId,
    string ChampionName,
    PlayerPosition Position,
    TeamSide RawTeam,
    bool IsDead,
    MinimapMarkerAffiliation Affiliation)
{
    public string ReferenceKey =>
        $"{NormalizeChampionKey(ChampionName)}:{Affiliation}";

    public static string NormalizeChampionKey(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}

internal sealed record ActiveMatchVisionReferenceResolution(
    string? ChampionId,
    ChampionPortraitReferenceStatus Status);
