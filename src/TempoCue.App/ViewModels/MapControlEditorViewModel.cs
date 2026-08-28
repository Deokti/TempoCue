using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Координирует проверяемые шаги редактора набора контроля карты.</summary>
public sealed class MapControlEditorViewModel : EditorWizardViewModel
{
    public MapControlEditorViewModel(
        LocalizationService localization,
        IGamePatchCatalog gamePatchCatalog,
        SaveMapControlPackUseCase saveMapControlPack,
        Action<TempoCue.Overlay.OverlayNotification>? showOverlayPreview = null)
        : base(localization, totalSteps: 5)
    {
        MainInformation = new MapControlMainInformationViewModel(
            localization.CurrentLanguageCode,
            gamePatchCatalog,
            localization);
        Scope = new MapControlScopeViewModel(localization);
        Content = new MapControlContentViewModel(localization);
        Verification = new MapControlVerificationViewModel(localization, showOverlayPreview);
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

    public string StepSummary => Localization.Get(CurrentStep switch
    {
        2 => "Coaching.MapControlEditor.StepSummary.Scope",
        3 => "Coaching.MapControlEditor.StepSummary.Content",
        4 => "Coaching.MapControlEditor.StepSummary.Verification",
        5 => "Coaching.MapControlEditor.StepSummary.Save",
        _ => "Coaching.MapControlEditor.StepSummary.Main"
    });

    public async Task BeginNewAsync(CancellationToken cancellationToken = default)
    {
        BeginNew();
        await MainInformation.EnsureGamePatchCatalogLoadedAsync(cancellationToken);
    }

    public void UpdateLanguage()
    {
        MainInformation.UpdateLanguage();
        Scope.UpdateLanguage();
        Content.UpdateLanguage();
        Verification.UpdateLanguage();
        Save.UpdateLanguage();
        OnPropertyChanged(nameof(StepSummary));
        NotifyModeLanguageChanged();
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
        ResetNavigation(
            createCopy ? MatchupEditorMode.Copy : MatchupEditorMode.Edit,
            createCopy ? 1 : TotalSteps);
    }

    private void BeginNew()
    {
        MainInformation.BeginNew();
        Scope.BeginNew();
        Content.BeginNew();
        Content.SetPatch(MainInformation.GamePatch);
        Save.BeginNew();
        ResetNavigation(MatchupEditorMode.Create);
    }

    private void Scope_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlScopeViewModel.IsComplete))
        {
            NotifyStepCompletionChanged();
        }
    }

    private void MainInformation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlMainInformationViewModel.IsComplete))
        {
            NotifyStepCompletionChanged();
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
            NotifyStepCompletionChanged();
        }
    }

    private void Verification_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlVerificationViewModel.CanContinue))
        {
            NotifyStepCompletionChanged();
        }
    }

    protected override bool IsStepComplete(int step) => step switch
    {
        1 => MainInformation.IsComplete,
        2 => Scope.IsComplete,
        3 => Content.IsComplete,
        4 => Verification.CanContinue,
        5 => Save.CanSave,
        _ => false
    };

    protected override void PrepareStep(int targetStep)
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

    private void RefreshVerification() =>
        Verification.Update(MainInformation, Scope, Content);

    protected override void OnCurrentStepChanged() =>
        OnPropertyChanged(nameof(StepSummary));
}
