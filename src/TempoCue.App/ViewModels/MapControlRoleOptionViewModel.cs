using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Одна выбираемая роль в области применения набора контроля карты.</summary>
public sealed class MapControlRoleOptionViewModel : ObservableObject
{
    private bool _isSelected;

    public MapControlRoleOptionViewModel(MapControlRole role, string displayName)
    {
        Role = role;
        DisplayName = displayName;
    }

    public MapControlRole Role { get; }

    public string DisplayName { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
