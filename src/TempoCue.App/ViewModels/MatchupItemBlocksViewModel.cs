using System.Collections.ObjectModel;
using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Владелец произвольных блоков предметов и их авторского порядка.</summary>
public sealed class MatchupItemBlocksViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ObservableCollection<MatchupItemBlockViewModel> _blocks = [];
    private int _nextBlockNumber = 1;

    public MatchupItemBlocksViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Blocks = new ReadOnlyObservableCollection<MatchupItemBlockViewModel>(_blocks);
        Editor = new MatchupItemBlockEditorViewModel(localization);
    }

    public ReadOnlyObservableCollection<MatchupItemBlockViewModel> Blocks { get; }

    public MatchupItemBlockEditorViewModel Editor { get; }

    public bool HasBlocks => _blocks.Count > 0;

    public int Count => _blocks.Count;

    public void BeginAdd() => Editor.BeginNew();

    public void BeginEdit(MatchupItemBlockViewModel block)
    {
        EnsureOwnedBlock(block);
        Editor.BeginEdit(block.CreateSnapshot());
    }

    public bool SaveEditor()
    {
        if (!Editor.CanSave)
        {
            return false;
        }

        if (Editor.EditingBlockId is string editingBlockId)
        {
            var existingBlock = _blocks.FirstOrDefault(block => block.Id == editingBlockId);
            if (existingBlock is null)
            {
                return false;
            }

            existingBlock.Replace(Editor.CreateSnapshot(editingBlockId));
        }
        else
        {
            _blocks.Add(new MatchupItemBlockViewModel(
                Editor.CreateSnapshot(CreateBlockId()),
                _localization));
        }

        Editor.Close();
        NotifyCollectionChanged();
        return true;
    }

    public MatchupItemBlockViewModel? DuplicateEditor()
    {
        if (!Editor.CanSave)
        {
            return null;
        }

        MatchupItemBlockDraft source;
        var sourceIndex = _blocks.Count - 1;
        if (Editor.EditingBlockId is string editingBlockId)
        {
            sourceIndex = FindBlockIndex(editingBlockId);
            if (sourceIndex < 0)
            {
                return null;
            }

            source = Editor.CreateSnapshot(editingBlockId);
        }
        else
        {
            source = Editor.CreateSnapshot(CreateBlockId());
            _blocks.Add(new MatchupItemBlockViewModel(source, _localization));
            sourceIndex = _blocks.Count - 1;
        }

        var copy = CreateDuplicate(source);
        _blocks.Insert(sourceIndex + 1, copy);
        Editor.BeginEdit(copy.CreateSnapshot());
        NotifyCollectionChanged();
        return copy;
    }

    public bool DeleteEditor()
    {
        if (Editor.EditingBlockId is not string editingBlockId)
        {
            Editor.Close();
            return true;
        }

        var blockIndex = FindBlockIndex(editingBlockId);
        return blockIndex >= 0 && Delete(_blocks[blockIndex]);
    }

    public MatchupItemBlockViewModel Duplicate(MatchupItemBlockViewModel block)
    {
        var sourceIndex = EnsureOwnedBlock(block);
        var copy = CreateDuplicate(block.CreateSnapshot());
        _blocks.Insert(sourceIndex + 1, copy);
        NotifyCollectionChanged();
        return copy;
    }

    public bool Delete(MatchupItemBlockViewModel block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var removed = _blocks.Remove(block);
        if (!removed)
        {
            return false;
        }

        if (Editor.EditingBlockId == block.Id)
        {
            Editor.Close();
        }

        NotifyCollectionChanged();
        return true;
    }

    public bool MoveUp(MatchupItemBlockViewModel block) => Move(block, -1);

    public bool MoveDown(MatchupItemBlockViewModel block) => Move(block, 1);

    public void SetItems(
        MatchupItemBlockViewModel block,
        IEnumerable<ItemOptionViewModel> selectedItems)
    {
        EnsureOwnedBlock(block);
        block.SetItems(selectedItems);
    }

    public bool RemoveItem(
        MatchupItemBlockViewModel block,
        MatchupItemDraft item)
    {
        EnsureOwnedBlock(block);
        return block.RemoveItem(item);
    }

    public bool MoveItemLeft(
        MatchupItemBlockViewModel block,
        MatchupItemDraft item)
    {
        EnsureOwnedBlock(block);
        return block.MoveItemLeft(item);
    }

    public bool MoveItemRight(
        MatchupItemBlockViewModel block,
        MatchupItemDraft item)
    {
        EnsureOwnedBlock(block);
        return block.MoveItemRight(item);
    }

    public void UpdateLanguage()
    {
        Editor.UpdateLanguage();
        foreach (var block in _blocks)
        {
            block.UpdateLanguage();
        }
    }

    private bool Move(MatchupItemBlockViewModel block, int offset)
    {
        var currentIndex = EnsureOwnedBlock(block);
        var targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= _blocks.Count)
        {
            return false;
        }

        _blocks.Move(currentIndex, targetIndex);
        NotifyCollectionChanged();
        return true;
    }

    private MatchupItemBlockViewModel CreateDuplicate(MatchupItemBlockDraft source)
    {
        var duplicateName = _localization.Format(
            "Coaching.MatchupEditor.Content.DuplicateName",
            source.Name);
        return new MatchupItemBlockViewModel(
            source with
            {
                Id = CreateBlockId(),
                Name = duplicateName.Length <= 80
                    ? duplicateName
                    : duplicateName[..80].TrimEnd(),
                Items = source.Items.ToArray()
            },
            _localization);
    }

    private int EnsureOwnedBlock(MatchupItemBlockViewModel block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var index = _blocks.IndexOf(block);
        if (index < 0)
        {
            throw new ArgumentException("Блок предметов не принадлежит текущему черновику.", nameof(block));
        }

        return index;
    }

    private int FindBlockIndex(string blockId)
    {
        for (var index = 0; index < _blocks.Count; index++)
        {
            if (_blocks[index].Id == blockId)
            {
                return index;
            }
        }

        return -1;
    }

    private string CreateBlockId() => $"item-block-{_nextBlockNumber++}";

    private void NotifyCollectionChanged()
    {
        for (var index = 0; index < _blocks.Count; index++)
        {
            _blocks[index].UpdatePosition(index, _blocks.Count);
        }

        OnPropertyChanged(nameof(HasBlocks));
        OnPropertyChanged(nameof(Count));
    }
}
