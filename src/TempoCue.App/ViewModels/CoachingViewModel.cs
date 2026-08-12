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
    private bool _isDisposed;

    public CoachingViewModel(
        LocalizationService localization,
        CoachingLibraryViewModel library,
        CoachingPackDetailsViewModel details,
        CoachPackImportDialogViewModel import,
        PackTypeSelectionViewModel packTypeSelection,
        MatchupEditorViewModel matchupEditor)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Library = library ?? throw new ArgumentNullException(nameof(library));
        Details = details ?? throw new ArgumentNullException(nameof(details));
        Import = import ?? throw new ArgumentNullException(nameof(import));
        PackTypeSelection = packTypeSelection
            ?? throw new ArgumentNullException(nameof(packTypeSelection));
        MatchupEditor = matchupEditor ?? throw new ArgumentNullException(nameof(matchupEditor));
        _localization.PropertyChanged += Localization_PropertyChanged;
    }

    public CoachingLibraryViewModel Library { get; }

    public CoachingPackDetailsViewModel Details { get; }

    public CoachPackImportDialogViewModel Import { get; }

    public PackTypeSelectionViewModel PackTypeSelection { get; }

    public MatchupEditorViewModel MatchupEditor { get; }

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
        Details.SelectForActions(card);
    }

    public async Task OpenPackDetailsAsync(
        CoachingPackCardViewModel card,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        IsDeleteConfirmationOpen = false;
        CurrentScreen = CoachingScreen.PackDetails;
        await Details.OpenAsync(card, cancellationToken);
    }

    public void OpenDeleteConfirmation()
    {
        if (Details.CanDelete)
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

    public async Task<bool> DeleteSelectedPackAsync(
        CancellationToken cancellationToken = default)
    {
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

    public void CloseMatchupEditor() =>
        CurrentScreen = MatchupEditor.ReturnsToPackDetails
            ? CoachingScreen.PackDetails
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
        Import.Dispose();
        Library.Dispose();
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MatchupEditor.UpdateLanguage(_localization.CurrentLanguageCode);
        if (Details.Pack is { } pack
            && Library.FindCard(pack.Id) is { } refreshedCard)
        {
            Details.UpdateCard(refreshedCard);
        }
    }
}
