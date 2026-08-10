using TempoCue.App.Localization;
using TempoCue.Application;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Переиспользуемое состояние модального каталога предметов. Получатель выбранного
/// предмета определяется целью открытия, а не логикой самого каталога.
/// </summary>
public sealed class ItemPickerViewModel : ObservableObject
{
    private readonly IItemCatalog _itemCatalog;
    private readonly LocalizationService _localization;
    private IReadOnlyList<ItemOptionViewModel> _items = [];
    private CatalogViewState _catalogState = CatalogViewState.NotLoaded;
    private string _gamePatch = string.Empty;
    private string _languageCode;
    private string? _loadedGamePatch;
    private string? _loadedLanguageCode;
    private ItemPickerTarget _target;
    private string _search = string.Empty;

    public ItemPickerViewModel(
        IItemCatalog itemCatalog,
        LocalizationService localization)
    {
        _itemCatalog = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _languageCode = localization.CurrentLanguageCode;
    }

    public event Action<ItemPickerTarget, ItemOptionViewModel>? ItemSelected;

    public IReadOnlyList<ItemOptionViewModel> Items => _items;

    public IReadOnlyList<ItemOptionViewModel> FilteredItems
    {
        get
        {
            var normalizedSearch = Search.Trim();
            if (normalizedSearch.Length == 0)
            {
                return Items;
            }

            return Items
                .Where(item => item.DisplayName.Contains(
                    normalizedSearch,
                    StringComparison.CurrentCultureIgnoreCase))
                .ToArray();
        }
    }

    public CatalogViewState CatalogState
    {
        get => _catalogState;
        private set
        {
            if (SetProperty(ref _catalogState, value))
            {
                OnPropertyChanged(nameof(IsCatalogReady));
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(HasLoadError));
                OnPropertyChanged(nameof(IsUsingCache));
            }
        }
    }

    public bool IsCatalogReady =>
        CatalogState is CatalogViewState.Ready or CatalogViewState.Cached;

    public bool IsLoading => CatalogState == CatalogViewState.Loading;

    public bool HasLoadError => CatalogState == CatalogViewState.Failed;

    public bool IsUsingCache => CatalogState == CatalogViewState.Cached;

    public ItemPickerTarget Target
    {
        get => _target;
        private set
        {
            if (SetProperty(ref _target, value))
            {
                OnPropertyChanged(nameof(IsOpen));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Description));
            }
        }
    }

    public bool IsOpen => Target != ItemPickerTarget.None;

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(FilteredItems));
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(ResultCountText));
            }
        }
    }

    public bool HasResults => FilteredItems.Count > 0;

    public string ResultCountText => _localization.Plural("Count.Item", FilteredItems.Count);

    public string CatalogStatusText => _localization.Format(
        "Coaching.MatchupEditor.Items.CatalogLoaded",
        Items.Count);

    public string Title => _localization.Get(Target switch
    {
        ItemPickerTarget.PhaseStart => "Coaching.MatchupEditor.Items.Picker.StartTitle",
        ItemPickerTarget.PhaseEnd => "Coaching.MatchupEditor.Items.Picker.EndTitle",
        _ => "Coaching.MatchupEditor.Items.Picker.Title"
    });

    public string Description => _localization.Get(Target switch
    {
        ItemPickerTarget.PhaseStart => "Coaching.MatchupEditor.Items.Picker.StartDescription",
        ItemPickerTarget.PhaseEnd => "Coaching.MatchupEditor.Items.Picker.EndDescription",
        _ => "Coaching.MatchupEditor.Items.Picker.Description"
    });

    public void SetContext(string gamePatch, string languageCode)
    {
        gamePatch ??= string.Empty;
        languageCode ??= _localization.CurrentLanguageCode;
        if (string.Equals(_gamePatch, gamePatch, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_languageCode, languageCode, StringComparison.OrdinalIgnoreCase))
        {
            NotifyLocalizedPresentationChanged();
            return;
        }

        Close();
        _gamePatch = gamePatch.Trim();
        _languageCode = languageCode;
        _items = [];
        _loadedGamePatch = null;
        _loadedLanguageCode = null;
        NotifyCatalogChanged();
        CatalogState = CatalogViewState.NotLoaded;
        NotifyLocalizedPresentationChanged();
    }

    public async Task EnsureCatalogLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_gamePatch)
            || IsLoading
            || (IsCatalogReady
                && string.Equals(_loadedGamePatch, _gamePatch, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_loadedLanguageCode, _languageCode, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        CatalogState = CatalogViewState.Loading;
        var requestedPatch = _gamePatch;
        var requestedLanguage = _languageCode;
        var result = await _itemCatalog.LoadAsync(
            requestedPatch,
            requestedLanguage,
            cancellationToken);

        if (!string.Equals(requestedPatch, _gamePatch, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(requestedLanguage, _languageCode, StringComparison.OrdinalIgnoreCase))
        {
            CatalogState = CatalogViewState.NotLoaded;
            return;
        }

        if (!result.IsAvailable)
        {
            _items = [];
            _loadedGamePatch = null;
            _loadedLanguageCode = null;
            NotifyCatalogChanged();
            CatalogState = CatalogViewState.Failed;
            return;
        }

        _items = result.Items
            .Select(item => new ItemOptionViewModel(
                item.Id,
                item.DisplayName,
                item.Description,
                item.ImageUri,
                item.Price,
                item.Tags))
            .ToArray();
        _loadedGamePatch = requestedPatch;
        _loadedLanguageCode = requestedLanguage;
        NotifyCatalogChanged();
        CatalogState = result.Source == ItemCatalogSource.Cache
            ? CatalogViewState.Cached
            : CatalogViewState.Ready;
    }

    public Task RetryCatalogLoadAsync(CancellationToken cancellationToken = default)
    {
        CatalogState = CatalogViewState.NotLoaded;
        return EnsureCatalogLoadedAsync(cancellationToken);
    }

    public void Open(ItemPickerTarget target)
    {
        if (!IsCatalogReady || target == ItemPickerTarget.None)
        {
            return;
        }

        Search = string.Empty;
        Target = target;
    }

    public void Select(ItemOptionViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!IsOpen)
        {
            return;
        }

        var selectedTarget = Target;
        Close();
        ItemSelected?.Invoke(selectedTarget, item);
    }

    public void Close()
    {
        Search = string.Empty;
        Target = ItemPickerTarget.None;
    }

    public void UpdateLanguage(string languageCode)
    {
        SetContext(_gamePatch, languageCode);
        NotifyLocalizedPresentationChanged();
    }

    private void NotifyCatalogChanged()
    {
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(FilteredItems));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(CatalogStatusText));
    }

    private void NotifyLocalizedPresentationChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(CatalogStatusText));
    }
}
