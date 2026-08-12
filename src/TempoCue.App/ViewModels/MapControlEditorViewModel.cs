using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Application;

namespace TempoCue.App.ViewModels;

/// <summary>Координирует проверяемые шаги редактора набора контроля карты.</summary>
public sealed class MapControlEditorViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private int _currentStep = 1;
    private int _highestVisitedStep = 1;

    public MapControlEditorViewModel(
        LocalizationService localization,
        IGamePatchCatalog gamePatchCatalog)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        MainInformation = new MapControlMainInformationViewModel(
            localization.CurrentLanguageCode,
            gamePatchCatalog,
            localization);
        Scope = new MapControlScopeViewModel(localization);
        MainInformation.PropertyChanged += MainInformation_PropertyChanged;
        Scope.PropertyChanged += Scope_PropertyChanged;
        BeginNew();
    }

    public MapControlMainInformationViewModel MainInformation { get; }

    public MapControlScopeViewModel Scope { get; }

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

        CurrentStep = targetStep;
        return true;
    }

    public bool CanNavigateToStep(int targetStep)
    {
        if (targetStep is < 1 or > 5
            || targetStep > Math.Min(TotalSteps, _highestVisitedStep + 1))
        {
            return false;
        }

        return targetStep == 1
            || targetStep == 2 && MainInformation.IsComplete
            || targetStep == 3 && MainInformation.IsComplete && Scope.IsComplete;
    }

    public void UpdateLanguage()
    {
        MainInformation.UpdateLanguage();
        Scope.UpdateLanguage();
        OnPropertyChanged(nameof(StepSummary));
    }

    private void BeginNew()
    {
        _highestVisitedStep = 1;
        CurrentStep = 1;
        MainInformation.BeginNew();
        Scope.BeginNew();
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
