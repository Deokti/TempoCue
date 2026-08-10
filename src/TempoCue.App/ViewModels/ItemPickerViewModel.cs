using System.Globalization;
using TempoCue.App.Localization;
using TempoCue.Application;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Переиспользуемое состояние модального каталога предметов. Получатель подтверждённого
/// предмета определяется целью открытия, а просмотр, фильтрация и сортировка остаются общими.
/// </summary>
public sealed class ItemPickerViewModel : ObservableObject
{
    private readonly IItemCatalog _itemCatalog;
    private readonly LocalizationService _localization;
    private IReadOnlyList<ItemOptionViewModel> _items = [];
    private IReadOnlyList<ItemCategoryOptionViewModel> _categoryOptions = [];
    private IReadOnlyList<ItemSortOptionViewModel> _sortOptions = [];
    private CatalogViewState _catalogState = CatalogViewState.NotLoaded;
    private string _gamePatch = string.Empty;
    private string _languageCode;
    private string? _loadedGamePatch;
    private string? _loadedLanguageCode;
    private ItemPickerTarget _target;
    private string _search = string.Empty;
    private ItemFilterCategory _selectedCategory = ItemFilterCategory.All;
    private ItemSortOptionViewModel? _selectedSortOption;
    private ItemOptionViewModel? _selectedItem;

    public ItemPickerViewModel(
        IItemCatalog itemCatalog,
        LocalizationService localization)
    {
        _itemCatalog = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _languageCode = localization.CurrentLanguageCode;
        RebuildFilterOptions();
    }

    public event Action<ItemPickerTarget, ItemOptionViewModel>? ItemSelected;

    public IReadOnlyList<ItemOptionViewModel> Items => _items;

    public IReadOnlyList<ItemCategoryOptionViewModel> CategoryOptions => _categoryOptions;

    public IReadOnlyList<ItemSortOptionViewModel> SortOptions => _sortOptions;

    public IReadOnlyList<ItemOptionViewModel> FilteredItems
    {
        get
        {
            var normalizedSearch = Search.Trim();
            var filtered = Items.Where(item =>
                (normalizedSearch.Length == 0
                 || item.DisplayName.Contains(
                     normalizedSearch,
                     StringComparison.CurrentCultureIgnoreCase))
                && MatchesCategory(item, SelectedCategory));

            return (SelectedSortOption?.Mode ?? ItemSortMode.Name) switch
            {
                ItemSortMode.PriceAscending => filtered
                    .OrderBy(static item => item.Price)
                    .ThenBy(static item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray(),
                ItemSortMode.PriceDescending => filtered
                    .OrderByDescending(static item => item.Price)
                    .ThenBy(static item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray(),
                _ => filtered
                    .OrderBy(static item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray()
            };
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
                NotifyFilteredItemsChanged();
            }
        }
    }

    public ItemFilterCategory SelectedCategory
    {
        get => _selectedCategory;
        private set
        {
            if (!SetProperty(ref _selectedCategory, value))
            {
                return;
            }

            foreach (var option in CategoryOptions)
            {
                option.IsSelected = option.Category == value;
            }

            SelectedItem = null;
            NotifyFilteredItemsChanged();
        }
    }

    public ItemSortOptionViewModel? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (value is not null && SetProperty(ref _selectedSortOption, value))
            {
                NotifyFilteredItemsChanged();
            }
        }
    }

    public ItemOptionViewModel? SelectedItem
    {
        get => _selectedItem;
        private set
        {
            if (ReferenceEquals(_selectedItem, value))
            {
                return;
            }

            if (_selectedItem is not null)
            {
                _selectedItem.IsSelected = false;
            }

            _selectedItem = value;
            if (_selectedItem is not null)
            {
                _selectedItem.IsSelected = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedItem));
            OnPropertyChanged(nameof(SelectedItemStats));
            OnPropertyChanged(nameof(HasSelectedItemStats));
            OnPropertyChanged(nameof(SelectedItemPriceText));
            OnPropertyChanged(nameof(SelectedItemTechnicalText));
        }
    }

    public bool HasSelectedItem => SelectedItem is not null;

    public IReadOnlyList<ItemStatViewModel> SelectedItemStats =>
        SelectedItem?.Stats.Select(CreateStatViewModel).ToArray() ?? [];

    public bool HasSelectedItemStats => SelectedItemStats.Count > 0;

    public string SelectedItemPriceText => SelectedItem is null
        ? string.Empty
        : _localization.Format(
            "Coaching.MatchupEditor.Items.Picker.Price",
            SelectedItem.Price);

    public string SelectedItemTechnicalText => SelectedItem is null
        ? string.Empty
        : _localization.Format(
            "Coaching.MatchupEditor.Items.Picker.Technical",
            SelectedItem.Id,
            GetCategoryLabel(SelectedItem.Category),
            _gamePatch);

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
                item.Tags,
                item.Category,
                item.Stats))
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
        SelectedCategory = ItemFilterCategory.All;
        SelectedSortOption = SortOptions.First(static option => option.Mode == ItemSortMode.Name);
        SelectedItem = null;
        Target = target;
    }

    public void SelectCategory(ItemCategoryOptionViewModel option)
    {
        ArgumentNullException.ThrowIfNull(option);
        SelectedCategory = option.Category;
    }

    public void Preview(ItemOptionViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsOpen)
        {
            SelectedItem = item;
        }
    }

    public bool ConfirmSelection()
    {
        if (!IsOpen || SelectedItem is null)
        {
            return false;
        }

        var selectedTarget = Target;
        var selectedItem = SelectedItem;
        Close();
        ItemSelected?.Invoke(selectedTarget, selectedItem);
        return true;
    }

    public void Close()
    {
        Search = string.Empty;
        SelectedItem = null;
        Target = ItemPickerTarget.None;
    }

    public void UpdateLanguage(string languageCode)
    {
        SetContext(_gamePatch, languageCode);
        RebuildFilterOptions();
        NotifyLocalizedPresentationChanged();
    }

    private void RebuildFilterOptions()
    {
        var selectedCategory = SelectedCategory;
        var selectedSortMode = SelectedSortOption?.Mode ?? ItemSortMode.Name;
        _categoryOptions = Enum.GetValues<ItemFilterCategory>()
            .Select(category => new ItemCategoryOptionViewModel(
                category,
                _localization.Get(GetFilterCategoryResourceKey(category))))
            .ToArray();
        foreach (var option in _categoryOptions)
        {
            option.IsSelected = option.Category == selectedCategory;
        }

        _sortOptions = Enum.GetValues<ItemSortMode>()
            .Select(mode => new ItemSortOptionViewModel(
                mode,
                _localization.Get(GetSortResourceKey(mode))))
            .ToArray();
        _selectedSortOption = _sortOptions.First(option => option.Mode == selectedSortMode);
        OnPropertyChanged(nameof(CategoryOptions));
        OnPropertyChanged(nameof(SortOptions));
        OnPropertyChanged(nameof(SelectedSortOption));
        NotifyFilteredItemsChanged();
    }

    private void NotifyCatalogChanged()
    {
        OnPropertyChanged(nameof(Items));
        NotifyFilteredItemsChanged();
        OnPropertyChanged(nameof(CatalogStatusText));
    }

    private void NotifyFilteredItemsChanged()
    {
        OnPropertyChanged(nameof(FilteredItems));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ResultCountText));
    }

    private void NotifyLocalizedPresentationChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(CatalogStatusText));
        OnPropertyChanged(nameof(SelectedItemStats));
        OnPropertyChanged(nameof(SelectedItemPriceText));
        OnPropertyChanged(nameof(SelectedItemTechnicalText));
    }

    private ItemStatViewModel CreateStatViewModel(ItemStatEntry stat) => new(
        _localization.Get(GetStatResourceKey(stat.Kind)),
        FormatStatValue(stat));

    private static string FormatStatValue(ItemStatEntry stat)
    {
        var suffix = stat.Kind is ItemStatKind.MoveSpeedPercent
            or ItemStatKind.AttackSpeedPercent
            or ItemStatKind.LifeStealPercent
            or ItemStatKind.CriticalStrikeChancePercent
            ? "%"
            : string.Empty;
        return $"+{stat.Value.ToString("0.#", CultureInfo.CurrentCulture)}{suffix}";
    }

    private string GetCategoryLabel(ItemCatalogCategory category) =>
        _localization.Get(category switch
        {
            ItemCatalogCategory.Starter => "Coaching.MatchupEditor.Items.Category.Starter",
            ItemCatalogCategory.Boots => "Coaching.MatchupEditor.Items.Category.Boots",
            ItemCatalogCategory.Epic => "Coaching.MatchupEditor.Items.Category.Epic",
            ItemCatalogCategory.Legendary => "Coaching.MatchupEditor.Items.Category.Legendary",
            _ => "Coaching.MatchupEditor.Items.Category.Basic"
        });

    private static bool MatchesCategory(
        ItemOptionViewModel item,
        ItemFilterCategory category) => category switch
    {
        ItemFilterCategory.All => true,
        ItemFilterCategory.Starter => item.Category == ItemCatalogCategory.Starter,
        ItemFilterCategory.Boots => item.Category == ItemCatalogCategory.Boots,
        ItemFilterCategory.Basic => item.Category == ItemCatalogCategory.Basic,
        ItemFilterCategory.Epic => item.Category == ItemCatalogCategory.Epic,
        ItemFilterCategory.Legendary => item.Category == ItemCatalogCategory.Legendary,
        _ => false
    };

    private static string GetFilterCategoryResourceKey(ItemFilterCategory category) => category switch
    {
        ItemFilterCategory.All => "Coaching.MatchupEditor.Items.Category.All",
        ItemFilterCategory.Starter => "Coaching.MatchupEditor.Items.Category.Starter",
        ItemFilterCategory.Boots => "Coaching.MatchupEditor.Items.Category.Boots",
        ItemFilterCategory.Basic => "Coaching.MatchupEditor.Items.Category.Basic",
        ItemFilterCategory.Epic => "Coaching.MatchupEditor.Items.Category.Epic",
        ItemFilterCategory.Legendary => "Coaching.MatchupEditor.Items.Category.Legendary",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    private static string GetSortResourceKey(ItemSortMode mode) => mode switch
    {
        ItemSortMode.Name => "Coaching.MatchupEditor.Items.Sort.Name",
        ItemSortMode.PriceAscending => "Coaching.MatchupEditor.Items.Sort.PriceAscending",
        ItemSortMode.PriceDescending => "Coaching.MatchupEditor.Items.Sort.PriceDescending",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static string GetStatResourceKey(ItemStatKind kind) => kind switch
    {
        ItemStatKind.Health => "Coaching.MatchupEditor.Items.Stat.Health",
        ItemStatKind.Mana => "Coaching.MatchupEditor.Items.Stat.Mana",
        ItemStatKind.AttackDamage => "Coaching.MatchupEditor.Items.Stat.AttackDamage",
        ItemStatKind.AbilityPower => "Coaching.MatchupEditor.Items.Stat.AbilityPower",
        ItemStatKind.Armor => "Coaching.MatchupEditor.Items.Stat.Armor",
        ItemStatKind.MagicResistance => "Coaching.MatchupEditor.Items.Stat.MagicResistance",
        ItemStatKind.MoveSpeed => "Coaching.MatchupEditor.Items.Stat.MoveSpeed",
        ItemStatKind.MoveSpeedPercent => "Coaching.MatchupEditor.Items.Stat.MoveSpeedPercent",
        ItemStatKind.AttackSpeedPercent => "Coaching.MatchupEditor.Items.Stat.AttackSpeed",
        ItemStatKind.LifeStealPercent => "Coaching.MatchupEditor.Items.Stat.LifeSteal",
        ItemStatKind.CriticalStrikeChancePercent => "Coaching.MatchupEditor.Items.Stat.CriticalStrikeChance",
        ItemStatKind.HealthRegen => "Coaching.MatchupEditor.Items.Stat.HealthRegen",
        ItemStatKind.ManaRegen => "Coaching.MatchupEditor.Items.Stat.ManaRegen",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
