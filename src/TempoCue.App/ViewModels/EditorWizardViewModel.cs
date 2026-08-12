using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Общий контракт навигации пошаговых редакторов.</summary>
public interface IEditorWizardViewModel
{
    int CurrentStep { get; }

    int CurrentStepIndex { get; }

    int TotalSteps { get; }

    bool NavigateToStep(int targetStep);
}

/// <summary>
/// Хранит одинаковые правила навигации редакторов: последовательное открытие шагов при создании
/// и свободный переход между шагами при редактировании существующего набора.
/// </summary>
public abstract class EditorWizardViewModel : ObservableObject, IEditorWizardViewModel
{
    private int _currentStep = 1;
    private int _highestVisitedStep = 1;
    private MatchupEditorMode _mode;

    protected EditorWizardViewModel(LocalizationService localization, int totalSteps)
    {
        Localization = localization ?? throw new ArgumentNullException(nameof(localization));
        if (totalSteps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalSteps));
        }

        TotalSteps = totalSteps;
    }

    protected LocalizationService Localization { get; }

    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                _highestVisitedStep = Math.Max(_highestVisitedStep, value);
                OnPropertyChanged(nameof(CurrentStepIndex));
                OnCurrentStepChanged();
                NotifyNavigationChanged();
            }
        }
    }

    public int CurrentStepIndex => CurrentStep - 1;

    public int TotalSteps { get; }

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

    public string ModeLabel => Localization.Get(Mode switch
    {
        MatchupEditorMode.Edit => "Coaching.MatchupEditor.Mode.Edit",
        MatchupEditorMode.Copy => "Coaching.MatchupEditor.Mode.Copy",
        _ => "Coaching.MatchupEditor.Draft"
    });

    public bool CanMovePrevious => CurrentStep > 1;

    public bool CanMoveNext => CurrentStep < TotalSteps && IsStepComplete(CurrentStep);

    public bool CanNavigateToStep1 => CanNavigateToStep(1);

    public bool CanNavigateToStep2 => CanNavigateToStep(2);

    public bool CanNavigateToStep3 => CanNavigateToStep(3);

    public bool CanNavigateToStep4 => CanNavigateToStep(4);

    public bool CanNavigateToStep5 => CanNavigateToStep(5);

    public bool MoveNext() => CanMoveNext && NavigateToStep(CurrentStep + 1);

    public bool MovePrevious() => CanMovePrevious && NavigateToStep(CurrentStep - 1);

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
        if (targetStep < 1 || targetStep > TotalSteps)
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

    protected void ResetNavigation(MatchupEditorMode mode, int highestVisitedStep = 1)
    {
        if (highestVisitedStep < 1 || highestVisitedStep > TotalSteps)
        {
            throw new ArgumentOutOfRangeException(nameof(highestVisitedStep));
        }

        Mode = mode;
        _highestVisitedStep = highestVisitedStep;
        if (CurrentStep != 1)
        {
            CurrentStep = 1;
            return;
        }

        OnCurrentStepChanged();
        NotifyNavigationChanged();
    }

    protected void NotifyStepCompletionChanged()
    {
        OnPropertyChanged(nameof(CanMoveNext));
        NotifyStepAvailabilityChanged();
    }

    protected void NotifyModeLanguageChanged() => OnPropertyChanged(nameof(ModeLabel));

    protected virtual void OnCurrentStepChanged()
    {
    }

    protected abstract bool IsStepComplete(int step);

    protected abstract void PrepareStep(int targetStep);

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
