using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Координирует проверяемые шаги редактора набора контроля карты.</summary>
public sealed class MapControlEditorViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private int _currentStep = 1;
    private int _highestVisitedStep = 1;
    private MatchupEditorMode _mode;

    public MapControlEditorViewModel(
        LocalizationService localization,
        IGamePatchCatalog gamePatchCatalog,
        SaveMapControlPackUseCase saveMapControlPack)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        MainInformation = new MapControlMainInformationViewModel(
            localization.CurrentLanguageCode,
            gamePatchCatalog,
            localization);
        Scope = new MapControlScopeViewModel(localization);
        Content = new MapControlContentViewModel(localization);
        Verification = new MapControlVerificationViewModel(localization);
        Save = new MapControlSaveViewModel(localization, saveMapControlPack);
        MainInformation.PropertyChanged += MainInformation_PropertyChanged;
        Scope.PropertyChanged += Scope_PropertyChanged;
        Content.PropertyChanged += Content_PropertyChanged;
        Verification.PropertyChanged += Verification_PropertyChanged;
        BeginNew();
    }

    public MapControlMainInformationViewModel MainInformation { get; }

    public MapControlScopeViewModel Scope { get; }

    public MapControlContentViewModel Content { get; }

    public MapControlVerificationViewModel Verification { get; }

    public MapControlSaveViewModel Save { get; }

    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                _highestVisitedStep = Math.Max(_highestVisitedStep, value);
                OnPropertyChanged(nameof(CurrentStepIndex));
                OnPropertyChanged(nameof(StepSummary));
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

    public string StepSummary => _localization.Get(CurrentStep switch
    {
        2 => "Coaching.MapControlEditor.StepSummary.Scope",
        3 => "Coaching.MapControlEditor.StepSummary.Content",
        4 => "Coaching.MapControlEditor.StepSummary.Verification",
        5 => "Coaching.MapControlEditor.StepSummary.Save",
        _ => "Coaching.MapControlEditor.StepSummary.Main"
    });

    public bool CanMovePrevious => CurrentStep > 1;

    public bool CanMoveNext => CurrentStep switch
    {
        1 => MainInformation.IsComplete,
        2 => Scope.IsComplete,
        3 => Content.IsComplete,
        4 => Verification.CanContinue,
        _ => false
    };

    public bool CanNavigateToStep1 => CanNavigateToStep(1);

    public bool CanNavigateToStep2 => CanNavigateToStep(2);

    public bool CanNavigateToStep3 => CanNavigateToStep(3);

    public bool CanNavigateToStep4 => CanNavigateToStep(4);

    public bool CanNavigateToStep5 => CanNavigateToStep(5);

    public async Task BeginNewAsync(CancellationToken cancellationToken = default)
    {
        BeginNew();
        await MainInformation.EnsureGamePatchCatalogLoadedAsync(cancellationToken);
    }

    public bool MoveNext() =>
        CanMoveNext && NavigateToStep(CurrentStep + 1);

    public bool MovePrevious() =>
        CanMovePrevious && NavigateToStep(CurrentStep - 1);

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

    public void UpdateLanguage()
    {
        MainInformation.UpdateLanguage();
        Scope.UpdateLanguage();
        Content.UpdateLanguage();
        Verification.UpdateLanguage();
        Save.UpdateLanguage();
        OnPropertyChanged(nameof(StepSummary));
        OnPropertyChanged(nameof(ModeLabel));
    }

    public async Task LoadAsync(
        MapControlPack pack,
        LocalCoachPackState state,
        bool createCopy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        await MainInformation.EnsureGamePatchCatalogLoadedAsync(cancellationToken);
        MainInformation.Load(pack, createCopy);
        Scope.Load(pack.Applicability);
        Content.SetPatch(pack.Patch);
        Content.Load(pack);
        Save.Load(pack, createCopy);
        Mode = createCopy ? MatchupEditorMode.Copy : MatchupEditorMode.Edit;
        _highestVisitedStep = createCopy ? 1 : TotalSteps;
        CurrentStep = 1;
        NotifyNavigationChanged();
    }

    private void BeginNew()
    {
        Mode = MatchupEditorMode.Create;
        _highestVisitedStep = 1;
        _currentStep = 1;
        MainInformation.BeginNew();
        Scope.BeginNew();
        Content.BeginNew();
        Content.SetPatch(MainInformation.GamePatch);
        Save.BeginNew();
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(CurrentStepIndex));
        OnPropertyChanged(nameof(StepSummary));
        NotifyNavigationChanged();
    }

    private void Scope_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlScopeViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanMoveNext));
            NotifyStepAvailabilityChanged();
        }
    }

    private void MainInformation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlMainInformationViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanMoveNext));
            NotifyStepAvailabilityChanged();
        }

        if (e.PropertyName == nameof(MapControlMainInformationViewModel.GamePatch))
        {
            Content.SetPatch(MainInformation.GamePatch);
        }
    }

    private void Content_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlContentViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanMoveNext));
            NotifyStepAvailabilityChanged();
        }
    }

    private void Verification_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlVerificationViewModel.CanContinue))
        {
            OnPropertyChanged(nameof(CanMoveNext));
            NotifyStepAvailabilityChanged();
        }
    }

    private bool IsStepComplete(int step) => step switch
    {
        1 => MainInformation.IsComplete,
        2 => Scope.IsComplete,
        3 => Content.IsComplete,
        4 => Verification.CanContinue,
        5 => Save.CanSave,
        _ => false
    };

    private void PrepareStep(int targetStep)
    {
        if (targetStep >= 3)
        {
            Content.SetPatch(MainInformation.GamePatch);
        }

        if (targetStep >= 4)
        {
            RefreshVerification();
        }

        if (targetStep >= 5)
        {
            Save.Update(MainInformation, Scope, Content);
        }
    }

    private void NotifyNavigationChanged()
    {
        OnPropertyChanged(nameof(CanMovePrevious));
        OnPropertyChanged(nameof(CanMoveNext));
        NotifyStepAvailabilityChanged();
    }

    private void RefreshVerification() =>
        Verification.Update(MainInformation, Scope, Content);

    private void NotifyStepAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanNavigateToStep1));
        OnPropertyChanged(nameof(CanNavigateToStep2));
        OnPropertyChanged(nameof(CanNavigateToStep3));
        OnPropertyChanged(nameof(CanNavigateToStep4));
        OnPropertyChanged(nameof(CanNavigateToStep5));
    }
}
