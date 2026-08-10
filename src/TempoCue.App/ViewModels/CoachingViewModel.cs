using System.ComponentModel;
using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Координирует переходы между самостоятельными экранами раздела коучинга.</summary>
public sealed class CoachingViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private CoachingScreen _currentScreen = CoachingScreen.Library;
    private bool _isDisposed;

    public CoachingViewModel(
        LocalizationService localization,
        CoachingLibraryViewModel library,
        PackTypeSelectionViewModel packTypeSelection,
        MatchupEditorViewModel matchupEditor)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Library = library ?? throw new ArgumentNullException(nameof(library));
        PackTypeSelection = packTypeSelection
            ?? throw new ArgumentNullException(nameof(packTypeSelection));
        MatchupEditor = matchupEditor ?? throw new ArgumentNullException(nameof(matchupEditor));
        _localization.PropertyChanged += Localization_PropertyChanged;
    }

    public CoachingLibraryViewModel Library { get; }

    public PackTypeSelectionViewModel PackTypeSelection { get; }

    public MatchupEditorViewModel MatchupEditor { get; }

    public CoachingScreen CurrentScreen
    {
        get => _currentScreen;
        private set => SetProperty(ref _currentScreen, value);
    }

    public void OpenPackTypeSelection() =>
        CurrentScreen = CoachingScreen.PackTypeSelection;

    public void OpenMatchupEditor() =>
        CurrentScreen = CoachingScreen.MatchupEditor;

    public void OpenLibrary() =>
        CurrentScreen = CoachingScreen.Library;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= Localization_PropertyChanged;
        Library.Dispose();
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        MatchupEditor.UpdateLanguage(_localization.CurrentLanguageCode);
}
