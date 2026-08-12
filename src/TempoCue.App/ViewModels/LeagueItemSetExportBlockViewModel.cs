namespace TempoCue.App.ViewModels;

/// <summary>Один выбираемый авторский блок в модальном окне экспорта.</summary>
public sealed class LeagueItemSetExportBlockViewModel : ObservableObject
{
    private bool _isSelected;

    public LeagueItemSetExportBlockViewModel(
        int order,
        CoachingPackItemBlockDetailsViewModel block)
    {
        ArgumentNullException.ThrowIfNull(block);
        Order = order;
        Id = block.Id;
        Name = block.Name;
        WhenToChoose = block.WhenToChoose;
        ItemCountLabel = block.ItemCountLabel;
        Items = block.Items;
        _isSelected = block.HasItems;
    }

    public int Order { get; }

    public string Id { get; }

    public string Name { get; }

    public string WhenToChoose { get; }

    public string ItemCountLabel { get; }

    public IReadOnlyList<CoachingPackItemDetailsViewModel> Items { get; }

    public bool HasItems => Items.Count > 0;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
