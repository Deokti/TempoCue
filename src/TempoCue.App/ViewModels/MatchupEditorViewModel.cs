using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Черновик пошагового редактора матчапа, пока не сохранённый на диск.</summary>
public sealed class MatchupEditorViewModel : EditorWizardViewModel
{
    public MatchupEditorViewModel(
        LocalizationService localization,
        IGamePatchCatalog gamePatchCatalog,
        IChampionCatalog championCatalog,
        IItemCatalog itemCatalog,
        SaveCoachPackUseCase saveCoachPack)
        : base(localization, totalSteps: 5)
    {
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

    public MatchupMainInformationViewModel MainInformation { get; }

    public MatchupChampionsAndRoleViewModel ChampionsAndRole { get; }

    public MatchupContentViewModel Content { get; }

    public MatchupVerificationViewModel Verification { get; }

    public MatchupSaveViewModel Save { get; }

    public void BeginNew()
    {
        MainInformation.BeginNew();
        ChampionsAndRole.BeginNew();
        Content.BeginNew();
        Save.BeginNew();
        ResetNavigation(MatchupEditorMode.Create);
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
        Content.SetItemCatalogContext(pack.Patch, Localization.CurrentLanguageCode);
        await Content.EnsureItemCatalogLoadedAsync(cancellationToken);
        Content.Load(pack);
        Save.Load(pack, state, createCopy);
        ResetNavigation(
            createCopy ? MatchupEditorMode.Copy : MatchupEditorMode.Edit,
            createCopy ? 1 : TotalSteps);
    }

    public void UpdateLanguage(string languageCode)
    {
        MainInformation.UpdateLanguage();
        ChampionsAndRole.UpdateLanguage(languageCode);
        Content.UpdateLanguage(languageCode);
        Verification.UpdateLanguage();
        Save.UpdateLanguage();
        NotifyModeLanguageChanged();
    }

    private void MainInformation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupMainInformationViewModel.IsComplete))
        {
            NotifyStepCompletionChanged();
        }
    }

    private void ChampionsAndRole_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupChampionsAndRoleViewModel.IsComplete))
        {
            NotifyStepCompletionChanged();
        }
    }

    private void Content_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupContentViewModel.IsComplete))
        {
            NotifyStepCompletionChanged();
        }
    }

    private void Verification_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MatchupVerificationViewModel.CanContinue))
        {
            NotifyStepCompletionChanged();
        }
    }

    protected override bool IsStepComplete(int step) => step switch
    {
        1 => MainInformation.IsComplete,
        2 => ChampionsAndRole.IsComplete,
        3 => Content.IsComplete,
        4 => Verification.CanContinue,
        5 => Save.CanSave,
        _ => false
    };

    protected override void PrepareStep(int targetStep)
    {
        if (targetStep >= 3)
        {
            Content.SetItemCatalogContext(
                MainInformation.GamePatch,
                Localization.CurrentLanguageCode);
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

}
