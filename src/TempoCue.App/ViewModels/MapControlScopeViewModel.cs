using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Управляет проверяемыми ограничениями ролей и стороны для Map Control Pack.</summary>
public sealed class MapControlScopeViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private MapControlTeamSide _selectedTeamSide;
    private bool _allRolesSelected;
    private bool _isRestoringRequiredRole;

    public MapControlScopeViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        RoleOptions =
        [
            new(MapControlRole.Top, "TOP"),
            new(MapControlRole.Jungle, "JUNGLE"),
            new(MapControlRole.Mid, "MID"),
            new(MapControlRole.Adc, "ADC"),
            new(MapControlRole.Support, "SUPPORT")
        ];

        foreach (var option in RoleOptions)
        {
            option.PropertyChanged += RoleOption_PropertyChanged;
        }

        BeginNew();
    }

    public IReadOnlyList<MapControlRoleOptionViewModel> RoleOptions { get; }

    public MapControlTeamSide SelectedTeamSide
    {
        get => _selectedTeamSide;
        set
        {
            if (SetProperty(ref _selectedTeamSide, value))
            {
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public bool AllRolesSelected => _allRolesSelected;

    public bool IsComplete => AllRolesSelected || RoleOptions.Any(static option => option.IsSelected);

    public IReadOnlyList<MapControlRole> SelectedRoles => AllRolesSelected
        ? MapControlRoleValues.All
        : RoleOptions
            .Where(static option => option.IsSelected)
            .Select(static option => option.Role)
            .ToArray();

    public string SummaryText
    {
        get
        {
            return _localization.Format(
                "Coaching.MapControlEditor.Scope.Summary",
                RolesSummary,
                TeamSideSummary);
        }
    }

    public string RolesSummary => AllRolesSelected
        ? _localization.Get("Coaching.MapControlEditor.Scope.Summary.AllRoles")
        : string.Join(", ", RoleOptions
            .Where(static option => option.IsSelected)
            .Select(static option => option.DisplayName));

    public string TeamSideSummary => _localization.Get(SelectedTeamSide switch
    {
        MapControlTeamSide.Blue => "Coaching.MapControlEditor.Scope.Summary.BlueSide",
        MapControlTeamSide.Red => "Coaching.MapControlEditor.Scope.Summary.RedSide",
        _ => "Coaching.MapControlEditor.Scope.Summary.AnySide"
    });

    public void SelectAllRoles()
    {
        _allRolesSelected = true;
        foreach (var option in RoleOptions)
        {
            option.IsSelected = false;
        }

        NotifySelectionChanged();
    }

    public void BeginNew()
    {
        SelectAllRoles();
        SelectedTeamSide = MapControlTeamSide.Any;
        NotifySelectionChanged();
    }

    public void UpdateLanguage()
    {
        OnPropertyChanged(nameof(RolesSummary));
        OnPropertyChanged(nameof(TeamSideSummary));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void RoleOption_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MapControlRoleOptionViewModel.IsSelected)
            || _isRestoringRequiredRole)
        {
            return;
        }

        if (sender is MapControlRoleOptionViewModel { IsSelected: true })
        {
            _allRolesSelected = false;
        }

        if (!_allRolesSelected
            && !RoleOptions.Any(static option => option.IsSelected)
            && sender is MapControlRoleOptionViewModel lastOption)
        {
            _isRestoringRequiredRole = true;
            lastOption.IsSelected = true;
            _isRestoringRequiredRole = false;
        }

        NotifySelectionChanged();
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(AllRolesSelected));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(SelectedRoles));
        OnPropertyChanged(nameof(RolesSummary));
        OnPropertyChanged(nameof(SummaryText));
    }
}
