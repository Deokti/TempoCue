namespace TempoCue.App.ViewModels;

/// <summary>Черновик пошагового редактора матчапа, пока не сохранённый на диск.</summary>
public sealed class MatchupEditorViewModel
{
    public MatchupEditorViewModel(string defaultLanguageCode)
    {
        MainInformation = new MatchupMainInformationViewModel(defaultLanguageCode);
    }

    public int CurrentStep => 1;

    public int CurrentStepIndex => CurrentStep - 1;

    public int TotalSteps => 7;

    public MatchupMainInformationViewModel MainInformation { get; }
}
