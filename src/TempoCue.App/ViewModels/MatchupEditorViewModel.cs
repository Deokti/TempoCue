using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Черновик пошагового редактора матчапа, пока не сохранённый на диск.</summary>
public sealed class MatchupEditorViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private int _currentStep = 1;
    private int _highestVisitedStep = 1;
    private MatchupEditorMode _mode;

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
            new MatchupPhaseSelector());
        Save = new MatchupSaveViewModel(localization, saveCoachPack);
        MainInformation.PropertyChanged += MainInformation_PropertyChanged;
        ChampionsAndRole.PropertyChanged += ChampionsAndRole_PropertyChanged;
        Content.PropertyChanged += Content_PropertyChanged;
        Verification.PropertyChanged += Verification_PropertyChanged;
        BeginNew();
    }

    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                _highestVisitedStep = Math.Max(_highestVisitedStep, value);
                OnPropertyChanged(nameof(CurrentStepIndex));
                NotifyNavigationChanged();
            }
        }
    }

    public int CurrentStepIndex => CurrentStep - 1;

    public int TotalSteps => 5;

    public MatchupEditorMode Mode
    {
        get => _mode;
        private set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsEditingExisting));
                OnPropertyChanged(nameof(ReturnsToPackDetails));
                OnPropertyChanged(nameof(ModeLabel));
                NotifyNavigationChanged();
            }
        }
    }

    public bool IsEditingExisting => Mode == MatchupEditorMode.Edit;

    public bool ReturnsToPackDetails => Mode is MatchupEditorMode.Edit or MatchupEditorMode.Copy;

    public string ModeLabel => _localization.Get(Mode switch
    {
        MatchupEditorMode.Edit => "Coaching.MatchupEditor.Mode.Edit",
        MatchupEditorMode.Copy => "Coaching.MatchupEditor.Mode.Copy",
        _ => "Coaching.MatchupEditor.Draft"
    });

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

    public bool CanNavigateToStep1 => CanNavigateToStep(1);

    public bool CanNavigateToStep2 => CanNavigateToStep(2);

    public bool CanNavigateToStep3 => CanNavigateToStep(3);

    public bool CanNavigateToStep4 => CanNavigateToStep(4);

    public bool CanNavigateToStep5 => CanNavigateToStep(5);

    public bool MoveNext()
    {
        return CanMoveNext && NavigateToStep(CurrentStep + 1);
    }

    public bool MovePrevious()
    {
        if (!CanMovePrevious)
        {
            return false;
        }

        return NavigateToStep(CurrentStep - 1);
    }

    public bool NavigateToStep(int targetStep)
    {
        if (!CanNavigateToStep(targetStep))
        {
            return false;
        }

        PrepareStep(targetStep);
        CurrentStep = targetStep;
        return true;
    }

    public bool CanNavigateToStep(int targetStep)
    {
        if (targetStep is < 1 or > 5)
        {
            return false;
        }

        if (Mode == MatchupEditorMode.Edit)
        {
            return true;
        }

        if (targetStep > Math.Min(TotalSteps, _highestVisitedStep + 1))
        {
            return false;
        }

        for (var step = 1; step < targetStep; step++)
        {
            if (!IsStepComplete(step))
            {
                return false;
            }
        }

        return true;
    }

    public void BeginNew()
    {
        Mode = MatchupEditorMode.Create;
        _highestVisitedStep = 1;
        CurrentStep = 1;
        MainInformation.BeginNew();
        ChampionsAndRole.BeginNew();
        Content.BeginNew();
        Save.BeginNew();
        NotifyNavigationChanged();
    }

    public async Task LoadAsync(
        CoachPack pack,
        LocalCoachPackState state,
        bool createCopy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        await Task.WhenAll(
            MainInformation.EnsureGamePatchCatalogLoadedAsync(cancellationToken),
            ChampionsAndRole.EnsureCatalogLoadedAsync(cancellationToken));

        MainInformation.Load(pack, createCopy);
        ChampionsAndRole.Load(pack);
        Content.SetItemCatalogContext(pack.Patch, _localization.CurrentLanguageCode);
        await Content.EnsureItemCatalogLoadedAsync(cancellationToken);
        Content.Load(pack);
        Save.Load(pack, state, createCopy);
        Mode = createCopy ? MatchupEditorMode.Copy : MatchupEditorMode.Edit;
        _highestVisitedStep = createCopy ? 1 : TotalSteps;
        CurrentStep = 1;
        NotifyNavigationChanged();
    }

    public void UpdateLanguage(string languageCode)
    {
        MainInformation.UpdateLanguage();
        ChampionsAndRole.UpdateLanguage(languageCode);
        Content.UpdateLanguage(languageCode);
        Verification.UpdateLanguage();
        Save.UpdateLanguage();
        OnPropertyChanged(nameof(ModeLabel));
    }

    private void MainInformation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupMainInformationViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanMoveNext));
            NotifyStepAvailabilityChanged();
        }
    }

    private void ChampionsAndRole_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupChampionsAndRoleViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanMoveNext));
            NotifyStepAvailabilityChanged();
        }
    }

    private void Content_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupContentViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanMoveNext));
            NotifyStepAvailabilityChanged();
        }
    }

    private void Verification_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupVerificationViewModel.CanContinue))
        {
            OnPropertyChanged(nameof(CanMoveNext));
            NotifyStepAvailabilityChanged();
        }
    }

    private bool IsStepComplete(int step) => step switch
    {
        1 => MainInformation.IsComplete,
        2 => ChampionsAndRole.IsComplete,
        3 => Content.IsComplete,
        4 => Verification.CanContinue,
        5 => Save.CanSave,
        _ => false
    };

    private void PrepareStep(int targetStep)
    {
        if (targetStep >= 3)
        {
            Content.SetItemCatalogContext(
                MainInformation.GamePatch,
                _localization.CurrentLanguageCode);
        }

        if (targetStep >= 4)
        {
            Verification.Update(MainInformation, ChampionsAndRole, Content);
        }

        if (targetStep >= 5)
        {
            Save.Update(MainInformation, ChampionsAndRole, Content);
        }
    }

    private void NotifyNavigationChanged()
    {
        OnPropertyChanged(nameof(CanMovePrevious));
        OnPropertyChanged(nameof(CanMoveNext));
        NotifyStepAvailabilityChanged();
    }

    private void NotifyStepAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanNavigateToStep1));
        OnPropertyChanged(nameof(CanNavigateToStep2));
        OnPropertyChanged(nameof(CanNavigateToStep3));
        OnPropertyChanged(nameof(CanNavigateToStep4));
        OnPropertyChanged(nameof(CanNavigateToStep5));
    }
}
