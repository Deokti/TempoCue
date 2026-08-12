using TempoCue.App.Localization;
using TempoCue.Application;

namespace TempoCue.App.ViewModels;

/// <summary>Общее состояние выбора игрового патча для редакторов Coach Pack.</summary>
public sealed class GamePatchSelectionViewModel : ObservableObject
{
    private readonly IGamePatchCatalog _catalog;
    private readonly LocalizationService _localization;
    private IReadOnlyList<GamePatchOptionViewModel> _options = [];
    private GamePatchOptionViewModel? _selectedOption;
    private CatalogViewState _state = CatalogViewState.NotLoaded;

    public GamePatchSelectionViewModel(
        IGamePatchCatalog catalog,
        LocalizationService localization)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public IReadOnlyList<GamePatchOptionViewModel> Options => _options;

    public GamePatchOptionViewModel? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetProperty(ref _selectedOption, value))
            {
                OnPropertyChanged(nameof(Patch));
                OnPropertyChanged(nameof(IsComplete));
            }
        }
    }

    public string Patch => SelectedOption?.Patch ?? string.Empty;

    public CatalogViewState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(IsUsingCache));
                OnPropertyChanged(nameof(IsComplete));
            }
        }
    }

    public bool IsReady => State is CatalogViewState.Ready or CatalogViewState.Cached;

    public bool IsLoading => State == CatalogViewState.Loading;

    public bool HasError => State == CatalogViewState.Failed;

    public bool IsUsingCache => State == CatalogViewState.Cached;

    public bool IsComplete => IsReady && SelectedOption is not null;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading || IsReady)
        {
            return;
        }

        State = CatalogViewState.Loading;
        var previouslySelectedPatch = SelectedOption?.Patch;
        var result = await _catalog.LoadAsync(cancellationToken);
        if (!result.IsAvailable)
        {
            _options = [];
            SelectedOption = null;
            OnPropertyChanged(nameof(Options));
            State = CatalogViewState.Failed;
            return;
        }

        _options = CreateOptions(result.Patches);
        OnPropertyChanged(nameof(Options));
        SelectedOption = Find(previouslySelectedPatch)
            ?? _options.FirstOrDefault(static option => option.IsCurrent)
            ?? _options.FirstOrDefault();
        State = result.IsFromCache
            ? CatalogViewState.Cached
            : CatalogViewState.Ready;
    }

    public Task RetryLoadAsync(CancellationToken cancellationToken = default)
    {
        State = CatalogViewState.NotLoaded;
        return EnsureLoadedAsync(cancellationToken);
    }

    public void ResetToCurrent() =>
        SelectedOption = _options.FirstOrDefault(static option => option.IsCurrent)
            ?? _options.FirstOrDefault();

    public void SelectPatch(string patch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patch);
        var option = Find(patch);
        if (option is null)
        {
            option = CreateOption(patch, false);
            _options = [option, .. _options];
            OnPropertyChanged(nameof(Options));
            State = CatalogViewState.Cached;
        }

        SelectedOption = option;
    }

    public void UpdateLanguage()
    {
        if (_options.Count == 0)
        {
            return;
        }

        var selectedPatch = SelectedOption?.Patch;
        _options = _options
            .Select(option => CreateOption(option.Patch, option.IsCurrent))
            .ToArray();
        OnPropertyChanged(nameof(Options));
        SelectedOption = Find(selectedPatch);
    }

    private IReadOnlyList<GamePatchOptionViewModel> CreateOptions(
        IReadOnlyList<GamePatchCatalogEntry> patches) =>
        patches
            .Select(patch => CreateOption(patch.Patch, patch.IsCurrent))
            .ToArray();

    private GamePatchOptionViewModel CreateOption(string patch, bool isCurrent) => new(
        patch,
        isCurrent
            ? _localization.Format("Coaching.MatchupEditor.Main.GamePatch.Current", patch)
            : patch,
        isCurrent);

    private GamePatchOptionViewModel? Find(string? patch) =>
        patch is null
            ? null
            : _options.FirstOrDefault(option =>
                string.Equals(option.Patch, patch, StringComparison.OrdinalIgnoreCase));
}
