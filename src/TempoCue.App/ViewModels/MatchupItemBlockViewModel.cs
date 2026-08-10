using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Сохранённый блок предметов текущего черновика.</summary>
public sealed class MatchupItemBlockViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private MatchupItemBlockDraft _draft;
    private int _order;
    private bool _canMoveUp;
    private bool _canMoveDown;

    public MatchupItemBlockViewModel(
        MatchupItemBlockDraft draft,
        LocalizationService localization)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public string Id => _draft.Id;

    public string Name => _draft.Name;

    public string? WhenToChoose => _draft.WhenToChoose;

    public IReadOnlyList<MatchupItemDraft> Items => _draft.Items;

    public int ItemCount => Items.Count;

    public string ItemCountLabel => _localization.Format(
        "Coaching.MatchupEditor.Items.Card.ItemCount",
        ItemCount);

    public string WhenToChooseLabel => WhenToChoose
        ?? _localization.Get("Coaching.MatchupEditor.Items.Card.NoHint");

    public int Order
    {
        get => _order;
        private set => SetProperty(ref _order, value);
    }

    public bool CanMoveUp
    {
        get => _canMoveUp;
        private set => SetProperty(ref _canMoveUp, value);
    }

    public bool CanMoveDown
    {
        get => _canMoveDown;
        private set => SetProperty(ref _canMoveDown, value);
    }

    internal MatchupItemBlockDraft CreateSnapshot() => _draft with
    {
        Items = _draft.Items.ToArray()
    };

    internal void Replace(MatchupItemBlockDraft draft)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        OnPropertyChanged(string.Empty);
    }

    internal void SetItems(IEnumerable<ItemOptionViewModel> selectedItems)
    {
        ArgumentNullException.ThrowIfNull(selectedItems);
        var selected = MatchupItemDraftFactory.Create(selectedItems);
        var selectedById = selected.ToDictionary(static item => item.Id);
        MatchupItemDraft[] orderedItems =
        [
            .. Items
                .Where(item => selectedById.ContainsKey(item.Id))
                .Select(item => selectedById[item.Id]),
            .. selected.Where(selectedItem => Items.All(item => item.Id != selectedItem.Id))
        ];
        ReplaceItems(orderedItems);
    }

    internal bool RemoveItem(MatchupItemDraft item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var updatedItems = Items.Where(existing => existing.Id != item.Id).ToArray();
        if (updatedItems.Length == Items.Count)
        {
            return false;
        }

        ReplaceItems(updatedItems);
        return true;
    }

    internal bool MoveItemLeft(MatchupItemDraft item) => MoveItem(item, -1);

    internal bool MoveItemRight(MatchupItemDraft item) => MoveItem(item, 1);

    internal void UpdatePosition(int index, int count)
    {
        Order = index + 1;
        CanMoveUp = index > 0;
        CanMoveDown = index < count - 1;
    }

    internal void UpdateLanguage()
    {
        OnPropertyChanged(nameof(ItemCountLabel));
        OnPropertyChanged(nameof(WhenToChooseLabel));
    }

    private bool MoveItem(MatchupItemDraft item, int offset)
    {
        ArgumentNullException.ThrowIfNull(item);
        var currentIndex = Items.ToList().FindIndex(existing => existing.Id == item.Id);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= Items.Count)
        {
            return false;
        }

        var updatedItems = Items.ToList();
        updatedItems.RemoveAt(currentIndex);
        updatedItems.Insert(targetIndex, item);
        ReplaceItems(updatedItems);
        return true;
    }

    private void ReplaceItems(IEnumerable<MatchupItemDraft> items)
    {
        _draft = _draft with { Items = items.ToArray() };
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(ItemCountLabel));
    }
}
