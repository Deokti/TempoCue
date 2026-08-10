using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Application;

namespace TempoCue.App.ViewModels;

/// <summary>Черновик пошагового редактора матчапа, пока не сохранённый на диск.</summary>
public sealed class MatchupEditorViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private int _currentStep = 1;

    public MatchupEditorViewModel(
        LocalizationService localization,
        IGamePatchCatalog gamePatchCatalog,
        IChampionCatalog championCatalog,
        IItemCatalog itemCatalog,
        SaveCoachPackUseCase saveCoachPack)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        MainInformation = new MatchupMainInformationViewModel(
            localization.CurrentLanguageCode,
            gamePatchCatalog,
            localization);
        ChampionsAndRole = new MatchupChampionsAndRoleViewModel(
            championCatalog,
            localization);
        Content = new MatchupContentViewModel(localization, itemCatalog);
        Verification = new MatchupVerificationViewModel(
            localization,
            new TempoCue.Core.MatchupPhaseSelector());
        Save = new MatchupSaveViewModel(localization, saveCoachPack);
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

    public MatchupVerificationViewModel Verification { get; }

    public MatchupSaveViewModel Save { get; }

    public bool CanMovePrevious => CurrentStep > 1;

    public bool CanMoveNext => CurrentStep switch
    {
        1 => MainInformation.IsComplete,
        2 => ChampionsAndRole.IsComplete,
        3 => Content.IsComplete,
        4 => Verification.CanContinue,
        _ => false
    };

    public bool MoveNext()
    {
        if (!CanMoveNext)
        {
            return false;
        }

        if (CurrentStep == 2)
        {
            Content.SetItemCatalogContext(
                MainInformation.GamePatch,
                _localization.CurrentLanguageCode);
        }
        else if (CurrentStep == 3)
        {
            Verification.Update(MainInformation, ChampionsAndRole, Content);
        }
        else if (CurrentStep == 4)
        {
            Save.Update(MainInformation, ChampionsAndRole, Content);
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
        Content.UpdateLanguage(languageCode);
        Verification.UpdateLanguage();
        Save.UpdateLanguage();
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
