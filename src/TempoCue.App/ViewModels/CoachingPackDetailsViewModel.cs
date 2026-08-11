using System.ComponentModel;
using System.Globalization;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Готовит сохранённый Coach Pack для просмотра без состояния редактора.</summary>
public sealed class CoachingPackDetailsViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private readonly IItemCatalog _itemCatalog;
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
    private long _itemCatalogLoadVersion;
    private bool _isDisposed;

    public CoachingPackDetailsViewModel(
        LocalizationService localization,
        IItemCatalog itemCatalog,
        BuildLeagueItemSetExportUseCase buildLeagueItemSetExport)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _itemCatalog = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
        Export = new LeagueItemSetExportDialogViewModel(
            localization,
            buildLeagueItemSetExport ?? throw new ArgumentNullException(nameof(buildLeagueItemSetExport)));
        _localization.PropertyChanged += Localization_PropertyChanged;
    }

    public LeagueItemSetExportDialogViewModel Export { get; }

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

    public string HeaderDetails => Pack is null
        ? string.Empty
        : _localization.Format(
            "Coaching.Details.Header",
            GetRoleLabel(Pack.LaneRole),
            GetAuthorLabel(Pack.Author),
            Card?.OriginLabel ?? string.Empty);

    public string Description => string.IsNullOrWhiteSpace(Pack?.Description)
        ? _localization.Get("Coaching.Details.Overview.Description.Empty")
        : Pack.Description;

    public string RoleLabel => Pack is null
        ? string.Empty
        : GetRoleLabel(Pack.LaneRole);

    public string AuthorLabel => GetAuthorLabel(Pack?.Author);

    public string ContentLanguageLabel => Card?.ContentLanguageLabel ?? string.Empty;

    public string PatchLabel => Pack?.Patch ?? string.Empty;

    public string VerifiedOnLabel => Pack?.VerifiedOn is DateOnly verifiedOn
        ? verifiedOn.ToString("D", CultureInfo.CurrentUICulture)
        : _localization.Get("Coaching.Details.Overview.NotSpecified");

    public string SourceLabel => BuildSourceLabel(Pack);

    public Uri? SourceUri => BuildSourceUri(Pack);

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
        private set => SetProperty(ref _itemBlocks, value);
    }

    public bool HasItemBlocks => Pack?.ItemBlocks.Count > 0;

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

        Phases = Pack.Rules
            .Select(rule => new CoachingPackPhaseDetailsViewModel(
                rule.Id,
                rule.Title,
                rule.Message,
                rule.WhyNow,
                rule.NextGoal,
                rule.PowerState,
                GetPowerStateLabel(rule.PowerState),
                GetLevelRange(rule.MinimumLevel, rule.MaximumLevel),
                ResolveItems(rule.RecommendedItemIds)))
            .ToArray();
        var phasesById = Phases.ToDictionary(static phase => phase.Id, StringComparer.Ordinal);
        PersistentTips = Pack.PersistentTips
            .Select(tip => new CoachingPackPersistentTipDetailsViewModel(
                tip.Title,
                tip.Message,
                tip.ApplicableRuleId is string ruleId && phasesById.TryGetValue(ruleId, out var phase)
                    ? phase.Title
                    : _localization.Get("Coaching.MatchupEditor.Persistent.Scope.WholeMatch")))
            .ToArray();
        ItemBlocks = Pack.ItemBlocks
            .Select(block => new CoachingPackItemBlockDetailsViewModel(
                block.Id,
                block.Name,
                string.IsNullOrWhiteSpace(block.WhenToChoose)
                    ? _localization.Get("Coaching.Details.Items.NoHint")
                    : block.WhenToChoose,
                _localization.Plural("Count.Item", block.ItemIds.Count),
                ResolveItems(block.ItemIds)))
            .ToArray();
        OnPropertyChanged(nameof(HasItemBlocks));
    }

    private IReadOnlyList<CoachingPackItemDetailsViewModel> ResolveItems(
        IEnumerable<int> itemIds) => itemIds
        .Select(itemId => _items.TryGetValue(itemId, out var item)
            ? new CoachingPackItemDetailsViewModel(item.Id, item.DisplayName, item.ImageUri)
            : new CoachingPackItemDetailsViewModel(
                itemId,
                _localization.Format("Coaching.Details.Items.Unknown", itemId),
                null))
        .ToArray();

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyHeaderChanged();
        RebuildContent();
        Export.UpdateLanguage();
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
        if (HasSourceLinkMessage)
        {
            SourceLinkMessage = _localization.Get("Coaching.Details.Source.OpenFailed");
        }
    }

    private string GetPowerStateLabel(PowerState powerState) => _localization.Get(powerState switch
    {
        PowerState.Dangerous => "Coaching.Power.Dangerous",
        PowerState.Cautious => "Coaching.Power.Cautious",
        PowerState.Playable => "Coaching.Power.Playable",
        PowerState.Favorable => "Coaching.Power.Favorable",
        _ => throw new ArgumentOutOfRangeException(nameof(powerState))
    });

    private string GetLevelRange(int minimumLevel, int? maximumLevel) =>
        maximumLevel is int upperBound
            ? _localization.Format("Coaching.Rule.LevelRange", minimumLevel, upperBound)
            : _localization.Format("Coaching.Rule.LevelFrom", minimumLevel);

    private string GetRoleLabel(string? role) => string.IsNullOrWhiteSpace(role)
        ? _localization.Get("Coaching.Library.Role.Any")
        : role.ToUpperInvariant();

    private string GetAuthorLabel(string? author) => string.IsNullOrWhiteSpace(author)
        ? _localization.Get("Coaching.Library.Author.TempoCue")
        : author;

    private readonly record struct ItemCatalogLoadRequest(
        long Version,
        CancellationTokenSource Cancellation);

    private string BuildSourceLabel(CoachPack? pack)
    {
        if (pack?.Source is null)
        {
            return pack?.Rules.Select(static rule => rule.Source)
                       .FirstOrDefault(static source => !string.IsNullOrWhiteSpace(source))
                   ?? _localization.Get("Coaching.Details.Overview.NotSpecified");
        }

        if (pack.Source.Kind == CoachPackSourceKind.PersonalExperience)
        {
            return _localization.Get("Coaching.Details.Source.PersonalExperience");
        }

        var title = string.IsNullOrWhiteSpace(pack.Source.Title)
            ? _localization.Get("Coaching.Details.Overview.NotSpecified")
            : pack.Source.Title;
        return string.IsNullOrWhiteSpace(pack.Source.Author)
            ? title
            : $"{title} — {pack.Source.Author}";
    }

    private static Uri? BuildSourceUri(CoachPack? pack)
    {
        if (!Uri.TryCreate(pack?.Source?.Url, UriKind.Absolute, out var sourceUri))
        {
            return null;
        }

        return string.Equals(sourceUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || string.Equals(sourceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? sourceUri
            : null;
    }
}
