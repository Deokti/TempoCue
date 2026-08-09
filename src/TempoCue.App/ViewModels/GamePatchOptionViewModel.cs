namespace TempoCue.App.ViewModels;

/// <summary>Патч и локализованная подпись для выбора в редакторе Coach Pack.</summary>
public sealed record GamePatchOptionViewModel(
    string Patch,
    string DisplayName,
    bool IsCurrent)
{
    public override string ToString() => DisplayName;
}
