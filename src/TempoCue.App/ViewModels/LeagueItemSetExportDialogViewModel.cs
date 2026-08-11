using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Владеет выбором блоков и предпросмотром одного экспорта в League.</summary>
public sealed class LeagueItemSetExportDialogViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly BuildLeagueItemSetExportUseCase _buildExport;
    private CancellationTokenSource? _buildCancellation;
    private CoachPack? _pack;
    private string _title = string.Empty;
    private IReadOnlyList<LeagueItemSetExportBlockViewModel> _blocks = [];
    private string _json = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isOpen;
    private LeagueItemSetExportError _lastError;
    private IReadOnlyList<int> _missingItemIds = [];
    private long _buildVersion;

    public LeagueItemSetExportDialogViewModel(
        LocalizationService localization,
        BuildLeagueItemSetExportUseCase buildExport)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _buildExport = buildExport ?? throw new ArgumentNullException(nameof(buildExport));
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value ?? string.Empty);
    }

    public string ChampionName { get; private set; } = string.Empty;

    public string MapName => _localization.Get("Coaching.Export.Map.SummonersRift");

    public IReadOnlyList<LeagueItemSetExportBlockViewModel> Blocks
    {
        get => _blocks;
        private set => SetProperty(ref _blocks, value);
    }

    public string SelectionSummary => string.Join(
        " · ",
        _localization.Plural("Count.ItemBlock", Blocks.Count(static block => block.IsSelected)),
        _localization.Plural(
            "Count.Item",
            Blocks.Where(static block => block.IsSelected)
                .Sum(static block => block.Items.Count)));

    public string Json
    {
        get => _json;
        private set
        {
            if (SetProperty(ref _json, value))
            {
                OnPropertyChanged(nameof(CanExport));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBuilding => Volatile.Read(ref _buildCancellation) is not null;

    public bool CanExport => !IsBuilding && !string.IsNullOrWhiteSpace(Json);

    public async Task OpenAsync(
        CoachPack pack,
        string title,
        IReadOnlyList<CoachingPackItemBlockDetailsViewModel> blocks,
        CancellationToken cancellationToken = default)
    {
        CancelCurrentBuild();
        _pack = pack ?? throw new ArgumentNullException(nameof(pack));
        ArgumentNullException.ThrowIfNull(blocks);
        Title = title;
        ChampionName = pack.PlayerChampion;
        Blocks = blocks
            .Select((block, index) => new LeagueItemSetExportBlockViewModel(index + 1, block))
            .ToArray();
        IsOpen = true;
        OnPropertyChanged(nameof(ChampionName));
        OnPropertyChanged(nameof(MapName));
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var request = BeginBuild(cancellationToken);
        var pack = _pack;
        var title = Title;
        var selectedBlockIds = Blocks
            .Where(static block => block.IsSelected)
            .Select(static block => block.Id)
            .ToArray();
        var hasSelectedEmptyBlock = Blocks.Any(
            static block => block.IsSelected && !block.HasItems);
        var languageCode = _localization.CurrentLanguageCode;

        if (pack is null)
        {
            CompleteBuild(request);
            return;
        }

        OnPropertyChanged(nameof(SelectionSummary));
        Json = string.Empty;
        _lastError = LeagueItemSetExportError.None;
        _missingItemIds = [];
        if (string.IsNullOrWhiteSpace(title))
        {
            StatusMessage = _localization.Get("Coaching.Export.Status.TitleRequired");
            CompleteBuild(request);
            return;
        }

        if (hasSelectedEmptyBlock)
        {
            StatusMessage = _localization.Get("Coaching.Export.Status.EmptyBlock");
            CompleteBuild(request);
            return;
        }

        StatusMessage = _localization.Get("Coaching.Export.Status.Building");
        try
        {
            var result = await _buildExport.ExecuteAsync(
                pack,
                title,
                selectedBlockIds,
                languageCode,
                request.Cancellation.Token);
            if (!IsCurrent(request))
            {
                return;
            }

            _lastError = result.Error;
            _missingItemIds = result.MissingItemIds;
            if (!result.IsSuccess)
            {
                StatusMessage = GetBuildErrorMessage();
                return;
            }

            Json = result.Json;
            StatusMessage = _localization.Format(
                "Coaching.Export.Status.Ready",
                _localization.Plural("Count.ItemBlock", result.BlockCount),
                _localization.Plural("Count.Item", result.ItemCount));
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested
            && !IsCurrent(request))
        {
            // Новый запрос или закрытие модального окна штатно отменяют устаревшую сборку.
        }
        finally
        {
            CompleteBuild(request);
        }
    }

    public void Close()
    {
        CancelCurrentBuild();
        IsOpen = false;
        _pack = null;
        Blocks = [];
        Json = string.Empty;
        StatusMessage = string.Empty;
    }

    public void MarkCopied() =>
        StatusMessage = _localization.Get("Coaching.Export.Status.Copied");

    public void MarkClipboardFailed() =>
        StatusMessage = _localization.Get("Coaching.Export.Status.ClipboardFailed");

    public void MarkDownloaded() =>
        StatusMessage = _localization.Get("Coaching.Export.Status.Downloaded");

    public void MarkFileWriteFailed() =>
        StatusMessage = _localization.Get("Coaching.Export.Status.FileWriteFailed");

    public void UpdateLanguage()
    {
        OnPropertyChanged(nameof(MapName));
        OnPropertyChanged(nameof(SelectionSummary));
        if (_lastError != LeagueItemSetExportError.None)
        {
            StatusMessage = GetBuildErrorMessage();
        }
    }

    private string GetBuildErrorMessage() => _lastError switch
    {
        LeagueItemSetExportError.NoBlocksSelected =>
            _localization.Get("Coaching.Export.Status.NoBlocks"),
        LeagueItemSetExportError.ChampionCatalogUnavailable =>
            _localization.Get("Coaching.Export.Status.ChampionCatalogUnavailable"),
        LeagueItemSetExportError.ChampionNotFound =>
            _localization.Get("Coaching.Export.Status.ChampionNotFound"),
        LeagueItemSetExportError.ItemCatalogUnavailable =>
            _localization.Get("Coaching.Export.Status.ItemCatalogUnavailable"),
        LeagueItemSetExportError.ItemsNotFound =>
            _localization.Format(
                "Coaching.Export.Status.ItemsNotFound",
                string.Join(", ", _missingItemIds)),
        _ => _localization.Get("Coaching.Export.Status.Failed")
    };

    private BuildRequest BeginBuild(CancellationToken cancellationToken)
    {
        var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        var version = Interlocked.Increment(ref _buildVersion);
        var previousCancellation = Interlocked.Exchange(
            ref _buildCancellation,
            requestCancellation);
        TryCancel(previousCancellation);
        NotifyBuildStateChanged();
        return new BuildRequest(version, requestCancellation);
    }

    private bool IsCurrent(BuildRequest request) =>
        Volatile.Read(ref _buildVersion) == request.Version
        && ReferenceEquals(Volatile.Read(ref _buildCancellation), request.Cancellation);

    private void CompleteBuild(BuildRequest request)
    {
        if (IsCurrent(request)
            && ReferenceEquals(
                Interlocked.CompareExchange(
                    ref _buildCancellation,
                    null,
                    request.Cancellation),
                request.Cancellation))
        {
            NotifyBuildStateChanged();
        }

        request.Cancellation.Dispose();
    }

    private void CancelCurrentBuild()
    {
        Interlocked.Increment(ref _buildVersion);
        var cancellation = Interlocked.Exchange(ref _buildCancellation, null);
        TryCancel(cancellation);
        NotifyBuildStateChanged();
    }

    private void NotifyBuildStateChanged()
    {
        OnPropertyChanged(nameof(IsBuilding));
        OnPropertyChanged(nameof(CanExport));
    }

    private static void TryCancel(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Завершившийся запрос уже освободил собственный источник отмены.
        }
    }

    private readonly record struct BuildRequest(
        long Version,
        CancellationTokenSource Cancellation);
}
