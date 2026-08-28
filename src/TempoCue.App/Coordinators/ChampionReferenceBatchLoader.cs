using System.Diagnostics;
using TempoCue.Application;
using TempoCue.Vision;

namespace TempoCue.App.Coordinators;

/// <summary>Готовит один согласованный набор портретов для состава активного матча.</summary>
internal sealed class ChampionReferenceBatchLoader
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(20);
    private readonly IChampionCatalog _championCatalog;
    private readonly IChampionPortraitReferenceLoader _portraitLoader;

    public ChampionReferenceBatchLoader(
        IChampionCatalog championCatalog,
        IChampionPortraitReferenceLoader portraitLoader)
    {
        _championCatalog = championCatalog ?? throw new ArgumentNullException(nameof(championCatalog));
        _portraitLoader = portraitLoader ?? throw new ArgumentNullException(nameof(portraitLoader));
    }

    public async Task<ChampionReferenceBatchResult> LoadAsync(
        ActiveMatchVisionParticipantContext context,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(LoadTimeout);
        var loadToken = timeoutCancellation.Token;

        try
        {
            var catalog = await _championCatalog.LoadAsync(
                context.LanguageCode,
                loadToken).ConfigureAwait(false);
            if (!catalog.IsAvailable)
            {
                return ChampionReferenceBatchResult.RetryableFailure(context);
            }

            var champions = BuildChampionLookup(catalog.Champions);
            var loadedParticipants = await Task.WhenAll(
                context.Players.Select(player =>
                    LoadPlayerReferenceAsync(player, champions, loadToken))).ConfigureAwait(false);
            var references = loadedParticipants
                .Where(static participant => participant.Reference is not null)
                .Select(static participant => participant.Reference!)
                .GroupBy(static reference => (reference.ChampionId, reference.Affiliation))
                .Select(static group => group.First())
                .ToArray();
            var resolutions = loadedParticipants
                .GroupBy(
                    static participant => participant.ReferenceKey,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    static group => group.Key,
                    static group =>
                    {
                        var participant = group.First();
                        return new ActiveMatchVisionReferenceResolution(
                            participant.ChampionId,
                            participant.Reference is null
                                ? ChampionPortraitReferenceStatus.Unavailable
                                : ChampionPortraitReferenceStatus.Ready);
                    },
                    StringComparer.OrdinalIgnoreCase);
            return new ChampionReferenceBatchResult(references, resolutions, ShouldRetry: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            Trace.TraceError(
                "Подготовка портретов участников превысила лимит {0} секунд.",
                LoadTimeout.TotalSeconds);
            return ChampionReferenceBatchResult.RetryableFailure(context);
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "Не удалось подготовить портреты участников для миникарты ({0}): {1}",
                exception.GetType().Name,
                exception.Message);
            return ChampionReferenceBatchResult.RetryableFailure(context);
        }
    }

    private async Task<LoadedParticipantReference> LoadPlayerReferenceAsync(
        ActiveMatchVisionParticipant player,
        IReadOnlyDictionary<string, ChampionCatalogEntry> champions,
        CancellationToken cancellationToken)
    {
        if (!champions.TryGetValue(
            ActiveMatchVisionParticipant.NormalizeChampionKey(player.ChampionName),
            out var champion))
        {
            Trace.TraceWarning(
                "В каталоге Data Dragon не найден чемпион {0}.",
                player.ChampionName);
            return new LoadedParticipantReference(player.ReferenceKey, null, null);
        }

        var reference = await _portraitLoader.LoadAsync(
            champion,
            player.Affiliation,
            cancellationToken).ConfigureAwait(false);
        return new LoadedParticipantReference(player.ReferenceKey, champion.Id, reference);
    }

    private static IReadOnlyDictionary<string, ChampionCatalogEntry> BuildChampionLookup(
        IReadOnlyList<ChampionCatalogEntry> champions)
    {
        var lookup = new Dictionary<string, ChampionCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var champion in champions)
        {
            lookup.TryAdd(ActiveMatchVisionParticipant.NormalizeChampionKey(champion.Id), champion);
            lookup.TryAdd(
                ActiveMatchVisionParticipant.NormalizeChampionKey(champion.DisplayName),
                champion);
        }

        return lookup;
    }

    private sealed record LoadedParticipantReference(
        string ReferenceKey,
        string? ChampionId,
        MinimapChampionReference? Reference);
}

internal sealed record ChampionReferenceBatchResult(
    IReadOnlyList<MinimapChampionReference> References,
    IReadOnlyDictionary<string, ActiveMatchVisionReferenceResolution> Resolutions,
    bool ShouldRetry)
{
    public static ChampionReferenceBatchResult RetryableFailure(
        ActiveMatchVisionParticipantContext context) =>
        new(
            [],
            context.Players
                .Select(static player => player.ReferenceKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    static referenceKey => referenceKey,
                    static _ => new ActiveMatchVisionReferenceResolution(
                        null,
                        ChampionPortraitReferenceStatus.Unavailable),
                    StringComparer.OrdinalIgnoreCase),
            ShouldRetry: true);
}
