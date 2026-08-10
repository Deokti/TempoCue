using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Владеет выбором блоков и предпросмотром одного экспорта в League.</summary>
public sealed class LeagueItemSetExportDialogViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly BuildLeagueItemSetExportUseCase _buildExport;
    private CoachPack? _pack;
    private string _title = string.Empty;
    private IReadOnlyList<LeagueItemSetExportBlockViewModel> _blocks = [];
    private string _json = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isOpen;
    private bool _isBuilding;
    private LeagueItemSetExportError _lastError;
    private IReadOnlyList<int> _missingItemIds = [];

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

    public bool IsBuilding
    {
        get => _isBuilding;
        private set
        {
            if (SetProperty(ref _isBuilding, value))
            {
                OnPropertyChanged(nameof(CanExport));
            }
        }
    }

    public bool CanExport => !IsBuilding && !string.IsNullOrWhiteSpace(Json);

    public async Task OpenAsync(
        CoachPack pack,
        string title,
        IReadOnlyList<CoachingPackItemBlockDetailsViewModel> blocks,
        CancellationToken cancellationToken = default)
    {
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
        if (_pack is null)
        {
            return;
        }

        OnPropertyChanged(nameof(SelectionSummary));
        Json = string.Empty;
        _lastError = LeagueItemSetExportError.None;
        _missingItemIds = [];
        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusMessage = _localization.Get("Coaching.Export.Status.TitleRequired");
            return;
        }

        if (Blocks.Any(static block => block.IsSelected && !block.HasItems))
        {
            StatusMessage = _localization.Get("Coaching.Export.Status.EmptyBlock");
            return;
        }

        IsBuilding = true;
        StatusMessage = _localization.Get("Coaching.Export.Status.Building");
        try
        {
            var result = await _buildExport.ExecuteAsync(
                _pack,
                Title,
                Blocks.Where(static block => block.IsSelected)
                    .Select(static block => block.Id)
                    .ToArray(),
                _localization.CurrentLanguageCode,
                cancellationToken);
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
        finally
        {
            IsBuilding = false;
        }
    }

    public void Close()
    {
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
}
