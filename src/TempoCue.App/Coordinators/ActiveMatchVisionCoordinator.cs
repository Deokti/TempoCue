using System.ComponentModel;
using System.Diagnostics;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Vision;

namespace TempoCue.App.Coordinators;

/// <summary>
/// Связывает участников активного матча с нейтральными эталонами портретов Vision.
/// </summary>
internal sealed class ActiveMatchVisionCoordinator : IDisposable
{
    private readonly IActiveMatchMonitor _monitor;
    private readonly IChampionCatalog _championCatalog;
    private readonly IChampionPortraitReferenceLoader _portraitLoader;
    private readonly IMinimapChampionReferenceSink _referenceSink;
    private readonly LocalizationService _localization;
    private readonly object _sync = new();

    private CancellationTokenSource? _loadCancellation;
    private string? _participantSignature;
    private int _generation;
    private bool _isDisposed;

    public ActiveMatchVisionCoordinator(
        IActiveMatchMonitor monitor,
        IChampionCatalog championCatalog,
        IChampionPortraitReferenceLoader portraitLoader,
        IMinimapChampionReferenceSink referenceSink,
        LocalizationService localization)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _championCatalog = championCatalog ?? throw new ArgumentNullException(nameof(championCatalog));
        _portraitLoader = portraitLoader ?? throw new ArgumentNullException(nameof(portraitLoader));
        _referenceSink = referenceSink ?? throw new ArgumentNullException(nameof(referenceSink));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));

        _monitor.Updated += OnMatchUpdated;
        _localization.PropertyChanged += OnLocalizationChanged;
        Schedule(_monitor.CurrentUpdate);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _generation++;
            _loadCancellation?.Cancel();
            _loadCancellation = null;
        }

        _monitor.Updated -= OnMatchUpdated;
        _localization.PropertyChanged -= OnLocalizationChanged;
    }

    private void OnMatchUpdated(object? sender, ActiveMatchUpdateEventArgs e) =>
        Schedule(e.Update);

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LocalizationService.CurrentLanguageCode))
        {
            lock (_sync)
            {
                _participantSignature = null;
            }

            Schedule(_monitor.CurrentUpdate);
        }
    }

    private void Schedule(ActiveMatchUpdate? update)
    {
        var context = CreateContext(update);
        CancellationTokenSource? cancellation;
        int generation;
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            if (context is not null &&
                string.Equals(
                    context.Signature,
                    _participantSignature,
                    StringComparison.Ordinal))
            {
                return;
            }

            _participantSignature = context?.Signature;
            _generation++;
            generation = _generation;
            _loadCancellation?.Cancel();
            _loadCancellation = context is null ? null : new CancellationTokenSource();
            cancellation = _loadCancellation;
        }

        if (context is null)
        {
            _referenceSink.SetChampionReferences([]);
            return;
        }

        _ = LoadAndPublishAsync(context, generation, cancellation!.Token);
    }

    private async Task LoadAndPublishAsync(
        ParticipantContext context,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await _championCatalog.LoadAsync(
                context.LanguageCode,
                cancellationToken).ConfigureAwait(false);
            if (!catalog.IsAvailable)
            {
                PublishIfCurrent(generation, []);
                return;
            }

            var champions = BuildChampionLookup(catalog.Champions);
            var loadingTasks = context.Players
                .Select(player => LoadPlayerReferenceAsync(
                    player,
                    champions,
                    cancellationToken))
                .ToArray();
            var loadedReferences = await Task.WhenAll(loadingTasks).ConfigureAwait(false);
            var references = loadedReferences
                .OfType<MinimapChampionReference>()
                .GroupBy(
                    static reference => (reference.ChampionId, reference.Affiliation))
                .Select(static group => group.First())
                .ToArray();
            PublishIfCurrent(generation, references);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "Не удалось подготовить портреты участников для миникарты ({0}): {1}",
                exception.GetType().Name,
                exception.Message);
            PublishIfCurrent(generation, []);
        }
    }

    private async Task<MinimapChampionReference?> LoadPlayerReferenceAsync(
        ParticipantReference player,
        IReadOnlyDictionary<string, ChampionCatalogEntry> champions,
        CancellationToken cancellationToken)
    {
        if (!champions.TryGetValue(NormalizeChampionKey(player.ChampionName), out var champion))
        {
            return null;
        }

        return await _portraitLoader.LoadAsync(
            champion,
            player.Affiliation,
            cancellationToken).ConfigureAwait(false);
    }

    private void PublishIfCurrent(
        int generation,
        IReadOnlyList<MinimapChampionReference> references)
    {
        lock (_sync)
        {
            if (_isDisposed || generation != _generation)
            {
                return;
            }
        }

        _referenceSink.SetChampionReferences(references);
    }

    private ParticipantContext? CreateContext(ActiveMatchUpdate? update)
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
            .Select(player => new ParticipantReference(
                player.ChampionName,
                player.Team == activePlayer.Team
                    ? MinimapMarkerAffiliation.Ally
                    : MinimapMarkerAffiliation.Enemy))
            .ToArray();
        if (players.Length == 0)
        {
            return null;
        }

        var languageCode = _localization.CurrentLanguageCode;
        var signature = string.Join(
            '|',
            players
                .OrderBy(static player => player.ChampionName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static player => player.Affiliation)
                .Select(static player => $"{player.Affiliation}:{player.ChampionName}"));
        return new ParticipantContext(
            $"{languageCode}|{signature}",
            languageCode,
            players);
    }

    private static IReadOnlyDictionary<string, ChampionCatalogEntry> BuildChampionLookup(
        IReadOnlyList<ChampionCatalogEntry> champions)
    {
        var lookup = new Dictionary<string, ChampionCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var champion in champions)
        {
            lookup.TryAdd(NormalizeChampionKey(champion.Id), champion);
            lookup.TryAdd(NormalizeChampionKey(champion.DisplayName), champion);
        }

        return lookup;
    }

    private static string NormalizeChampionKey(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private sealed record ParticipantContext(
        string Signature,
        string LanguageCode,
        IReadOnlyList<ParticipantReference> Players);

    private sealed record ParticipantReference(
        string ChampionName,
        MinimapMarkerAffiliation Affiliation);
}
