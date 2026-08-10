using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Application;

namespace TempoCue.App.ViewModels;

/// <summary>Черновик пошагового редактора матчапа, пока не сохранённый на диск.</summary>
public sealed class MatchupEditorViewModel : ObservableObject
{
    private int _currentStep = 1;

    public MatchupEditorViewModel(
        LocalizationService localization,
        IGamePatchCatalog gamePatchCatalog,
        IChampionCatalog championCatalog)
    {
        MainInformation = new MatchupMainInformationViewModel(
            localization.CurrentLanguageCode,
            gamePatchCatalog,
            localization);
        ChampionsAndRole = new MatchupChampionsAndRoleViewModel(
            championCatalog,
            localization);
        Content = new MatchupContentViewModel(localization);
        MainInformation.PropertyChanged += MainInformation_PropertyChanged;
        ChampionsAndRole.PropertyChanged += ChampionsAndRole_PropertyChanged;
        Content.PropertyChanged += Content_PropertyChanged;
    }

    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(CurrentStepIndex));
                OnPropertyChanged(nameof(CanMovePrevious));
                OnPropertyChanged(nameof(CanMoveNext));
            }
        }
    }

    public int CurrentStepIndex => CurrentStep - 1;

    public int TotalSteps => 5;

    public MatchupMainInformationViewModel MainInformation { get; }

    public MatchupChampionsAndRoleViewModel ChampionsAndRole { get; }

    public MatchupContentViewModel Content { get; }

    public bool CanMovePrevious => CurrentStep > 1;

    public bool CanMoveNext => CurrentStep switch
    {
        1 => MainInformation.IsComplete,
        2 => ChampionsAndRole.IsComplete,
        _ => false
    };

    public bool MoveNext()
    {
        if (!CanMoveNext)
        {
            return false;
        }

        CurrentStep++;
        return true;
    }

    public bool MovePrevious()
    {
        if (!CanMovePrevious)
        {
            return false;
        }

        CurrentStep--;
        return true;
    }

    public void UpdateLanguage(string languageCode)
    {
        MainInformation.UpdateLanguage();
        ChampionsAndRole.UpdateLanguage(languageCode);
        Content.UpdateLanguage();
    }

    private void MainInformation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupMainInformationViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanMoveNext));
        }
    }

    private void ChampionsAndRole_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupChampionsAndRoleViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanMoveNext));
        }
    }

    private void Content_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupContentViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanMoveNext));
        }
    }
}
