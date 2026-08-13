using System.ComponentModel;
using System.IO;
using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Координирует переходы между самостоятельными экранами раздела коучинга.</summary>
public sealed class CoachingViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private CoachingScreen _currentScreen = CoachingScreen.Library;
    private bool _isDeleteConfirmationOpen;
    private bool _isMapControlActionSelected;
    private bool _isDisposed;

    public CoachingViewModel(
        LocalizationService localization,
        CoachingLibraryViewModel library,
        CoachingPackDetailsViewModel details,
        MapControlPackDetailsViewModel mapControlDetails,
        CoachPackImportDialogViewModel import,
        PackTypeSelectionViewModel packTypeSelection,
        MatchupEditorViewModel matchupEditor,
        MapControlEditorViewModel mapControlEditor)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Library = library ?? throw new ArgumentNullException(nameof(library));
        Details = details ?? throw new ArgumentNullException(nameof(details));
        MapControlDetails = mapControlDetails
            ?? throw new ArgumentNullException(nameof(mapControlDetails));
        Import = import ?? throw new ArgumentNullException(nameof(import));
        PackTypeSelection = packTypeSelection
            ?? throw new ArgumentNullException(nameof(packTypeSelection));
        MatchupEditor = matchupEditor ?? throw new ArgumentNullException(nameof(matchupEditor));
        MapControlEditor = mapControlEditor
            ?? throw new ArgumentNullException(nameof(mapControlEditor));
        _localization.PropertyChanged += Localization_PropertyChanged;
    }

    public CoachingLibraryViewModel Library { get; }

    public CoachingPackDetailsViewModel Details { get; }

    public MapControlPackDetailsViewModel MapControlDetails { get; }

    public CoachPackImportDialogViewModel Import { get; }

    public PackTypeSelectionViewModel PackTypeSelection { get; }

    public MatchupEditorViewModel MatchupEditor { get; }

    public MapControlEditorViewModel MapControlEditor { get; }

    public CoachingScreen CurrentScreen
    {
        get => _currentScreen;
        private set => SetProperty(ref _currentScreen, value);
    }

    public bool IsDeleteConfirmationOpen
    {
        get => _isDeleteConfirmationOpen;
        private set => SetProperty(ref _isDeleteConfirmationOpen, value);
    }

    public void OpenPackTypeSelection() =>
        CurrentScreen = CoachingScreen.PackTypeSelection;

    public Task OpenImportAsync(
        Stream source,
        CancellationToken cancellationToken = default) =>
        Import.OpenAsync(source, cancellationToken);

    public void ReportImportFileFailure() => Import.OpenFileFailure();

    public async Task<bool> ConfirmImportAsync(CancellationToken cancellationToken = default)
    {
        var packName = Import.PackName;
        if (!await Import.ConfirmAsync(cancellationToken))
        {
            return false;
        }

        Import.Close();
        await Library.RefreshAsync(cancellationToken);
        Library.ReportImportSuccess(packName);
        return true;
    }

    public void CloseImport() => Import.Close();

    public void SelectPackForActions(CoachingPackCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _isMapControlActionSelected = false;
        Details.SelectForActions(card);
        NotifyDeleteTextChanged();
    }

    public void SelectMapControlPackForActions(MapControlPackCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _isMapControlActionSelected = true;
        MapControlDetails.SelectForActions(card);
        NotifyDeleteTextChanged();
    }

    public async Task OpenPackDetailsAsync(
        CoachingPackCardViewModel card,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        IsDeleteConfirmationOpen = false;
        _isMapControlActionSelected = false;
        CurrentScreen = CoachingScreen.PackDetails;
        await Details.OpenAsync(card, cancellationToken);
        NotifyDeleteTextChanged();
    }

    public void OpenMapControlPackDetails(MapControlPackCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        IsDeleteConfirmationOpen = false;
        _isMapControlActionSelected = true;
        MapControlDetails.Open(card);
        CurrentScreen = CoachingScreen.MapControlPackDetails;
        NotifyDeleteTextChanged();
    }

    public void OpenDeleteConfirmation()
    {
        if (_isMapControlActionSelected ? MapControlDetails.CanDelete : Details.CanDelete)
        {
            IsDeleteConfirmationOpen = true;
        }
    }

    public void CloseDeleteConfirmation() =>
        IsDeleteConfirmationOpen = false;

    public async Task<bool> SetSelectedPackEnabledAsync(
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var card = Details.Card;
        if (card is null
            || !await Library.SetPackEnabledAsync(card, isEnabled, cancellationToken))
        {
            return false;
        }

        var refreshedCard = Library.FindCard(card.Pack.Id);
        if (refreshedCard is not null)
        {
            Details.UpdateCard(refreshedCard);
        }

        return true;
    }

    public async Task<bool> SetSelectedMapControlPackEnabledAsync(
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var card = MapControlDetails.Card;
        if (card is null
            || !await Library.SetMapControlPackEnabledAsync(card, isEnabled, cancellationToken))
        {
            return false;
        }

        var refreshedCard = Library.FindMapControlCard(card.Pack.Id);
        if (refreshedCard is not null)
        {
            MapControlDetails.UpdateCard(refreshedCard);
        }

        return true;
    }

    public async Task<bool> DeleteSelectedPackAsync(
        CancellationToken cancellationToken = default)
    {
        if (_isMapControlActionSelected)
        {
            var mapCard = MapControlDetails.Card;
            if (mapCard is null
                || !mapCard.CanDelete
                || !await Library.DeleteMapControlPackAsync(mapCard, cancellationToken))
            {
                return false;
            }

            IsDeleteConfirmationOpen = false;
            CurrentScreen = CoachingScreen.Library;
            return true;
        }

        var card = Details.Card;
        if (card is null
            || !card.CanDelete
            || !await Library.DeletePackAsync(card, cancellationToken))
        {
            return false;
        }

        Details.Export.Close();
        IsDeleteConfirmationOpen = false;
        CurrentScreen = CoachingScreen.Library;
        return true;
    }

    public void OpenMatchupEditor() =>
        OpenNewMatchupEditor();

    public void OpenNewMatchupEditor()
    {
        MatchupEditor.BeginNew();
        CurrentScreen = CoachingScreen.MatchupEditor;
    }

    public string DeleteConfirmationTitle => _isMapControlActionSelected
        ? MapControlDetails.DeleteConfirmationTitle
        : Details.DeleteConfirmationTitle;

    public string DeleteConfirmationMessage => _isMapControlActionSelected
        ? MapControlDetails.DeleteConfirmationMessage
        : Details.DeleteConfirmationMessage;

    public string DeleteConfirmationActionLabel => _isMapControlActionSelected
        ? MapControlDetails.DeleteConfirmationActionLabel
        : Details.DeleteConfirmationActionLabel;

    public string DeleteFailureTitle => _isMapControlActionSelected
        ? MapControlDetails.DeleteFailureTitle
        : Details.DeleteFailureTitle;

    public string DeleteFailureMessage => _isMapControlActionSelected
        ? MapControlDetails.DeleteFailureMessage
        : Details.DeleteFailureMessage;

    public async Task OpenNewMapControlEditorAsync(
        CancellationToken cancellationToken = default)
    {
        var initialization = MapControlEditor.BeginNewAsync(cancellationToken);
        CurrentScreen = CoachingScreen.MapControlEditor;
        await initialization;
    }

    public async Task OpenSelectedPackEditorAsync(
        CancellationToken cancellationToken = default)
    {
        var card = Details.Card
            ?? throw new InvalidOperationException("Coach Pack для редактирования не выбран.");
        await MatchupEditor.LoadAsync(
            card.Pack,
            card.State,
            card.CanCreateCopy,
            cancellationToken);
        CurrentScreen = CoachingScreen.MatchupEditor;
    }

    public async Task OpenSelectedMapControlPackEditorAsync(
        CancellationToken cancellationToken = default)
    {
        var card = MapControlDetails.Card
            ?? throw new InvalidOperationException("Map Control Pack для редактирования не выбран.");
        await MapControlEditor.LoadAsync(
            card.Pack,
            card.State,
            card.CanCreateCopy,
            cancellationToken);
        CurrentScreen = CoachingScreen.MapControlEditor;
    }

    public void CloseMatchupEditor() =>
        CurrentScreen = MatchupEditor.ReturnsToPackDetails
            ? CoachingScreen.PackDetails
            : CoachingScreen.PackTypeSelection;

    public void CloseMapControlEditor() =>
        CurrentScreen = MapControlEditor.ReturnsToPackDetails
            ? CoachingScreen.MapControlPackDetails
            : CoachingScreen.PackTypeSelection;

    public void OpenLibrary()
    {
        IsDeleteConfirmationOpen = false;
        CurrentScreen = CoachingScreen.Library;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= Localization_PropertyChanged;
        Details.Dispose();
        MapControlDetails.Dispose();
        Import.Dispose();
        Library.Dispose();
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MatchupEditor.UpdateLanguage(_localization.CurrentLanguageCode);
        MapControlEditor.UpdateLanguage();
        if (Details.Pack is { } pack
            && Library.FindCard(pack.Id) is { } refreshedCard)
        {
            Details.UpdateCard(refreshedCard);
        }

        if (MapControlDetails.Pack is { } mapPack
            && Library.FindMapControlCard(mapPack.Id) is { } refreshedMapCard)
        {
            MapControlDetails.UpdateCard(refreshedMapCard);
        }

        NotifyDeleteTextChanged();
    }

    private void NotifyDeleteTextChanged()
    {
        OnPropertyChanged(nameof(DeleteConfirmationTitle));
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
        OnPropertyChanged(nameof(DeleteConfirmationActionLabel));
        OnPropertyChanged(nameof(DeleteFailureTitle));
        OnPropertyChanged(nameof(DeleteFailureMessage));
    }
}
