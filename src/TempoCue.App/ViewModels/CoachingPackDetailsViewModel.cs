using System.ComponentModel;
using System.IO;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Готовит сохранённый Coach Pack для просмотра без состояния редактора.</summary>
public sealed class CoachingPackDetailsViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private readonly IItemCatalog _itemCatalog;
    private readonly CoachingPackDetailsContentFactory _contentFactory;
    private CancellationTokenSource? _itemCatalogLoadCancellation;
    private CoachingPackCardViewModel? _card;
    private IReadOnlyDictionary<int, ItemCatalogEntry> _items =
        new Dictionary<int, ItemCatalogEntry>();
    private CoachingPackDetailsTab _currentTab = CoachingPackDetailsTab.Overview;
    private IReadOnlyList<CoachingPackPhaseDetailsViewModel> _phases = [];
    private IReadOnlyList<CoachingPackPersistentTipDetailsViewModel> _persistentTips = [];
    private IReadOnlyList<CoachingPackItemBlockDetailsViewModel> _itemBlocks = [];
    private bool _isItemCatalogLoading;
    private string _itemCatalogMessage = string.Empty;
    private string _sourceLinkMessage = string.Empty;
    private CoachPackExportStatus _coachPackExportStatus;
    private bool _isCoachPackExporting;
    private long _itemCatalogLoadVersion;
    private bool _isDisposed;

    public CoachingPackDetailsViewModel(
        LocalizationService localization,
        IItemCatalog itemCatalog,
        BuildLeagueItemSetExportUseCase buildLeagueItemSetExport,
        ExportCoachPackUseCase exportCoachPack)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _itemCatalog = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
        _contentFactory = new CoachingPackDetailsContentFactory(localization);
        Export = new LeagueItemSetExportDialogViewModel(
            localization,
            buildLeagueItemSetExport ?? throw new ArgumentNullException(nameof(buildLeagueItemSetExport)));
        CoachPackExport = exportCoachPack ?? throw new ArgumentNullException(nameof(exportCoachPack));
        _localization.PropertyChanged += Localization_PropertyChanged;
    }

    public LeagueItemSetExportDialogViewModel Export { get; }

    private ExportCoachPackUseCase CoachPackExport { get; }

    public CoachingPackCardViewModel? Card => _card;

    public CoachPack? Pack => Card?.Pack;

    public bool HasPack => Pack is not null;

    public string Title => Card?.Title ?? string.Empty;

    public string PlayerChampionName => Card?.PlayerChampionName ?? string.Empty;

    public string OpponentChampionName => Card?.OpponentChampionName ?? string.Empty;

    public string PlayerChampionApplicability => _localization.Format(
        "Coaching.Details.Overview.Value.MyChampion",
        PlayerChampionName);

    public string OpponentChampionApplicability => _localization.Format(
        "Coaching.Details.Overview.Value.Opponent",
        OpponentChampionName);

    public string RoleApplicability => _localization.Format(
        "Coaching.Details.Overview.Value.Role",
        RoleLabel);

    public string PlayerChampionInitials => Card?.PlayerChampionInitials ?? "—";

    public string OpponentChampionInitials => Card?.OpponentChampionInitials ?? "—";

    public Uri? PlayerChampionImageUri => Card?.PlayerChampionImageUri;

    public Uri? OpponentChampionImageUri => Card?.OpponentChampionImageUri;

    public string StateLabel => Card?.StateLabel ?? string.Empty;

    public bool IsEnabled => Card?.IsEnabled == true;

    public string StateActionLabel => IsEnabled
        ? _localization.Get("Coaching.Details.Disable")
        : _localization.Get("Coaching.Details.Enable");

    public string EditActionLabel => Card?.CanEdit == true
        ? _localization.Get("Coaching.Details.Edit")
        : _localization.Get("Coaching.Details.CreateCopy");

    public bool CanDelete => Card?.CanDelete == true;

    public bool IsImported => Card?.Origin == CoachingPackOrigin.Imported;

    public string DeleteActionLabel => IsImported
        ? _localization.Get("Coaching.Details.Delete.Imported")
        : _localization.Get("Coaching.Details.Delete.Own");

    public string DeleteConfirmationTitle => IsImported
        ? _localization.Get("Coaching.Details.Delete.Confirmation.Imported.Title")
        : _localization.Get("Coaching.Details.Delete.Confirmation.Own.Title");

    public string DeleteConfirmationMessage => _localization.Format(
        IsImported
            ? "Coaching.Details.Delete.Confirmation.Imported.Message"
            : "Coaching.Details.Delete.Confirmation.Own.Message",
        Title);

    public string DeleteConfirmationActionLabel => IsImported
        ? _localization.Get("Coaching.Details.Delete.Confirmation.Imported.Action")
        : _localization.Get("Coaching.Details.Delete.Confirmation.Own.Action");

    public string DeleteFailureTitle =>
        _localization.Get("Coaching.Details.Delete.Failed.Title");

    public string DeleteFailureMessage =>
        _localization.Get("Coaching.Details.Delete.Failed.Message");

    public string HeaderDetails => Pack is null
        ? string.Empty
        : _localization.Format(
            "Coaching.Details.Header",
            GetRoleLabel(Pack.LaneRole),
            CoachPackDetailsPresentation.Author(_localization, Pack.Author),
            Card?.OriginLabel ?? string.Empty);

    public string Description =>
        CoachPackDetailsPresentation.Description(_localization, Pack?.Description);

    public string RoleLabel => Pack is null
        ? string.Empty
        : GetRoleLabel(Pack.LaneRole);

    public string AuthorLabel =>
        CoachPackDetailsPresentation.Author(_localization, Pack?.Author);

    public string ContentLanguageLabel => Card?.ContentLanguageLabel ?? string.Empty;

    public string PatchLabel => Pack?.Patch ?? string.Empty;

    public string VerifiedOnLabel =>
        CoachPackDetailsPresentation.VerifiedOn(_localization, Pack?.VerifiedOn);

    public string SourceLabel => CoachPackDetailsPresentation.SourceLabel(
        _localization,
        Pack?.Source,
        Pack?.Rules.Select(static rule => rule.Source)
            .FirstOrDefault(static source => !string.IsNullOrWhiteSpace(source)));

    public Uri? SourceUri => CoachPackDetailsPresentation.SourceUri(Pack?.Source);

    public bool HasSourceUri => SourceUri is not null;

    public string SourceLinkMessage
    {
        get => _sourceLinkMessage;
        private set
        {
            if (SetProperty(ref _sourceLinkMessage, value))
            {
                OnPropertyChanged(nameof(HasSourceLinkMessage));
            }
        }
    }

    public bool HasSourceLinkMessage => !string.IsNullOrWhiteSpace(SourceLinkMessage);

    public bool IsCoachPackExporting
    {
        get => _isCoachPackExporting;
        private set
        {
            if (SetProperty(ref _isCoachPackExporting, value))
            {
                OnPropertyChanged(nameof(CanExportCoachPack));
            }
        }
    }

    public bool CanExportCoachPack => Pack is not null && !IsCoachPackExporting;

    public string CoachPackExportMessage => _coachPackExportStatus switch
    {
        CoachPackExportStatus.Saved => _localization.Get("Coaching.Details.ExportPack.Status.Saved"),
        CoachPackExportStatus.Failed => _localization.Get("Coaching.Details.ExportPack.Status.Failed"),
        _ => string.Empty
    };

    public bool HasCoachPackExportMessage => _coachPackExportStatus != CoachPackExportStatus.None;

    public bool HasCoachPackExportError => _coachPackExportStatus == CoachPackExportStatus.Failed;

    public string CoachPackExportDialogFilter =>
        _localization.Get("Coaching.Details.ExportPack.Filter");

    public CoachingPackDetailsTab CurrentTab
    {
        get => _currentTab;
        private set
        {
            if (SetProperty(ref _currentTab, value))
            {
                OnPropertyChanged(nameof(IsOverviewTab));
                OnPropertyChanged(nameof(IsAdviceTab));
                OnPropertyChanged(nameof(IsItemsTab));
            }
        }
    }

    public bool IsOverviewTab => CurrentTab == CoachingPackDetailsTab.Overview;

    public bool IsAdviceTab => CurrentTab == CoachingPackDetailsTab.Advice;

    public bool IsItemsTab => CurrentTab == CoachingPackDetailsTab.Items;

    public IReadOnlyList<CoachingPackPhaseDetailsViewModel> Phases
    {
        get => _phases;
        private set
        {
            if (SetProperty(ref _phases, value))
            {
                OnPropertyChanged(nameof(HasPhases));
            }
        }
    }

    public bool HasPhases => Phases.Count > 0;

    public IReadOnlyList<CoachingPackPersistentTipDetailsViewModel> PersistentTips
    {
        get => _persistentTips;
        private set
        {
            if (SetProperty(ref _persistentTips, value))
            {
                OnPropertyChanged(nameof(HasPersistentTips));
            }
        }
    }

    public bool HasPersistentTips => PersistentTips.Count > 0;

    public IReadOnlyList<CoachingPackItemBlockDetailsViewModel> ItemBlocks
    {
        get => _itemBlocks;
        private set
        {
            if (SetProperty(ref _itemBlocks, value))
            {
                OnPropertyChanged(nameof(HasItemBlocks));
            }
        }
    }

    public bool HasItemBlocks => ItemBlocks.Count > 0;

    public bool IsItemCatalogLoading
    {
        get => _isItemCatalogLoading;
        private set => SetProperty(ref _isItemCatalogLoading, value);
    }

    public string ItemCatalogMessage
    {
        get => _itemCatalogMessage;
        private set
        {
            if (SetProperty(ref _itemCatalogMessage, value))
            {
                OnPropertyChanged(nameof(HasItemCatalogMessage));
            }
        }
    }

    public bool HasItemCatalogMessage => !string.IsNullOrWhiteSpace(ItemCatalogMessage);

    public async Task OpenAsync(
        CoachingPackCardViewModel card,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(card);

        var request = BeginItemCatalogLoad(cancellationToken);
        try
        {
            _card = card;
            _items = new Dictionary<int, ItemCatalogEntry>();
            IsItemCatalogLoading = card.Pack.ItemBlocks.Count > 0;
            ItemCatalogMessage = string.Empty;
            SourceLinkMessage = string.Empty;
            SetCoachPackExportStatus(CoachPackExportStatus.None);
            CurrentTab = CoachingPackDetailsTab.Overview;
            NotifyHeaderChanged();
            RebuildContent();
            await LoadItemCatalogAsync(card.Pack, request);
        }
        finally
        {
            CompleteItemCatalogLoad(request);
        }
    }

    public void UpdateCard(CoachingPackCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (Pack?.Id != card.Pack.Id)
        {
            throw new ArgumentException("Карточка относится к другому Coach Pack.", nameof(card));
        }

        _card = card;
        NotifyHeaderChanged();
    }

    /// <summary>Выбирает карточку для быстрого действия, не загружая каталог предметов.</summary>
    public void SelectForActions(CoachingPackCardViewModel card)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(card);
        CancelCurrentItemCatalogLoad();
        _card = card;
        SourceLinkMessage = string.Empty;
        SetCoachPackExportStatus(CoachPackExportStatus.None);
        NotifyHeaderChanged();
    }

    public void ShowOverview() => CurrentTab = CoachingPackDetailsTab.Overview;

    public void ReportSourceLinkOpenFailure() =>
        SourceLinkMessage = _localization.Get("Coaching.Details.Source.OpenFailed");

    public void ShowAdvice() => CurrentTab = CoachingPackDetailsTab.Advice;

    public void ShowItems()
    {
        if (HasItemBlocks)
        {
            CurrentTab = CoachingPackDetailsTab.Items;
        }
    }

    public Task OpenExportAsync(CancellationToken cancellationToken = default)
    {
        if (Pack is null || !HasItemBlocks)
        {
            return Task.CompletedTask;
        }

        return Export.OpenAsync(
            Pack,
            _localization.Format("Coaching.Export.DefaultTitle", Title),
            ItemBlocks,
            cancellationToken);
    }

    public async Task<bool> ExportCoachPackAsync(
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (Pack is null || IsCoachPackExporting)
        {
            return false;
        }

        IsCoachPackExporting = true;
        SetCoachPackExportStatus(CoachPackExportStatus.None);
        try
        {
            await CoachPackExport.ExecuteAsync(Pack, destination, cancellationToken);
            SetCoachPackExportStatus(CoachPackExportStatus.Saved);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            SetCoachPackExportStatus(CoachPackExportStatus.Failed);
            return false;
        }
        finally
        {
            IsCoachPackExporting = false;
        }
    }

    public void ReportCoachPackExportFileFailure() =>
        SetCoachPackExportStatus(CoachPackExportStatus.Failed);

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        CancelCurrentItemCatalogLoad();
        Export.Close();
        _localization.PropertyChanged -= Localization_PropertyChanged;
    }

    private async Task LoadItemCatalogAsync(CoachPack pack, ItemCatalogLoadRequest request)
    {
        if (pack.ItemBlocks.Count == 0)
        {
            return;
        }

        try
        {
            var result = await _itemCatalog.LoadAsync(
                pack.Patch,
                _localization.CurrentLanguageCode,
                request.Cancellation.Token);
            if (!IsCurrentItemCatalogLoad(request))
            {
                return;
            }

            if (!result.IsAvailable)
            {
                ItemCatalogMessage = _localization.Get("Coaching.Details.Items.CatalogUnavailable");
                return;
            }

            _items = result.Items.ToDictionary(static item => item.Id);
            RebuildContent();
        }
        catch (OperationCanceledException) when (request.Cancellation.IsCancellationRequested)
        {
        }
    }

    private ItemCatalogLoadRequest BeginItemCatalogLoad(CancellationToken cancellationToken)
    {
        var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var version = Interlocked.Increment(ref _itemCatalogLoadVersion);
        var previousCancellation = Interlocked.Exchange(
            ref _itemCatalogLoadCancellation,
            requestCancellation);
        TryCancel(previousCancellation);
        return new ItemCatalogLoadRequest(version, requestCancellation);
    }

    private bool IsCurrentItemCatalogLoad(ItemCatalogLoadRequest request) =>
        !_isDisposed
        && Volatile.Read(ref _itemCatalogLoadVersion) == request.Version
        && ReferenceEquals(
            Volatile.Read(ref _itemCatalogLoadCancellation),
            request.Cancellation);

    private void CompleteItemCatalogLoad(ItemCatalogLoadRequest request)
    {
        if (IsCurrentItemCatalogLoad(request)
            && ReferenceEquals(
                Interlocked.CompareExchange(
                    ref _itemCatalogLoadCancellation,
                    null,
                    request.Cancellation),
                request.Cancellation))
        {
            IsItemCatalogLoading = false;
        }

        request.Cancellation.Dispose();
    }

    private void CancelCurrentItemCatalogLoad()
    {
        Interlocked.Increment(ref _itemCatalogLoadVersion);
        var cancellation = Interlocked.Exchange(ref _itemCatalogLoadCancellation, null);
        TryCancel(cancellation);
    }

    private static void TryCancel(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Завершившийся запрос мог уже освободить собственный источник отмены.
        }
    }

    private void RebuildContent()
    {
        if (Pack is null)
        {
            Phases = [];
            PersistentTips = [];
            ItemBlocks = [];
            return;
        }

        var content = _contentFactory.Create(Pack, _items);
        Phases = content.Phases;
        PersistentTips = content.PersistentTips;
        ItemBlocks = content.ItemBlocks;
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyHeaderChanged();
        RebuildContent();
        Export.UpdateLanguage();
        OnPropertyChanged(nameof(CoachPackExportMessage));
        OnPropertyChanged(nameof(CoachPackExportDialogFilter));
    }

    private void NotifyHeaderChanged()
    {
        OnPropertyChanged(nameof(Card));
        OnPropertyChanged(nameof(Pack));
        OnPropertyChanged(nameof(HasPack));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PlayerChampionName));
        OnPropertyChanged(nameof(OpponentChampionName));
        OnPropertyChanged(nameof(PlayerChampionApplicability));
        OnPropertyChanged(nameof(OpponentChampionApplicability));
        OnPropertyChanged(nameof(RoleApplicability));
        OnPropertyChanged(nameof(PlayerChampionInitials));
        OnPropertyChanged(nameof(OpponentChampionInitials));
        OnPropertyChanged(nameof(PlayerChampionImageUri));
        OnPropertyChanged(nameof(OpponentChampionImageUri));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(StateActionLabel));
        OnPropertyChanged(nameof(EditActionLabel));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(IsImported));
        OnPropertyChanged(nameof(DeleteActionLabel));
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
        OnPropertyChanged(nameof(DeleteConfirmationActionLabel));
        OnPropertyChanged(nameof(DeleteFailureTitle));
        OnPropertyChanged(nameof(DeleteFailureMessage));
        OnPropertyChanged(nameof(HeaderDetails));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(RoleLabel));
        OnPropertyChanged(nameof(AuthorLabel));
        OnPropertyChanged(nameof(ContentLanguageLabel));
        OnPropertyChanged(nameof(PatchLabel));
        OnPropertyChanged(nameof(VerifiedOnLabel));
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(SourceUri));
        OnPropertyChanged(nameof(HasSourceUri));
        OnPropertyChanged(nameof(CanExportCoachPack));
        if (HasSourceLinkMessage)
        {
            SourceLinkMessage = _localization.Get("Coaching.Details.Source.OpenFailed");
        }
    }

    private string GetRoleLabel(string? role) => string.IsNullOrWhiteSpace(role)
        ? _localization.Get("Coaching.Library.Role.Any")
        : role.ToUpperInvariant();

    private readonly record struct ItemCatalogLoadRequest(
        long Version,
        CancellationTokenSource Cancellation);

    private void SetCoachPackExportStatus(CoachPackExportStatus status)
    {
        if (_coachPackExportStatus == status)
        {
            return;
        }

        _coachPackExportStatus = status;
        OnPropertyChanged(nameof(CoachPackExportMessage));
        OnPropertyChanged(nameof(HasCoachPackExportMessage));
        OnPropertyChanged(nameof(HasCoachPackExportError));
    }

    private enum CoachPackExportStatus
    {
        None,
        Saved,
        Failed
    }
}
