using TempoCue.App.Localization;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Представляет сохранённую справочную подсказку текущего черновика.</summary>
public sealed class MapControlPersistentTipViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private MapControlPersistentTip _tip;

    public MapControlPersistentTipViewModel(
        MapControlPersistentTip tip,
        LocalizationService localization)
    {
        _tip = tip ?? throw new ArgumentNullException(nameof(tip));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public string Id => _tip.Id;

    public string Title => _tip.Title;

    public string Message => _tip.Message;

    public MapControlPersistentTipScope Scope => _tip.Scope;

    public string ScopeLabel => MapControlContentText.PersistentTipScopeName(
        Scope,
        _localization);

    internal MapControlPersistentTip Tip => _tip;

    internal void Replace(MapControlPersistentTip tip)
    {
        _tip = tip ?? throw new ArgumentNullException(nameof(tip));
        OnPropertyChanged(string.Empty);
    }

    internal void UpdateLanguage()
    {
        OnPropertyChanged(nameof(ScopeLabel));
    }
}
