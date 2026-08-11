using System.Windows;
using System.Windows.Controls;

namespace TempoCue.App.Views;

/// <summary>Переиспользуемое меню редких действий над Coach Pack.</summary>
public partial class CoachPackActionsMenu : UserControl
{
    public static readonly DependencyProperty CanExportProperty = DependencyProperty.Register(
        nameof(CanExport),
        typeof(bool),
        typeof(CoachPackActionsMenu),
        new PropertyMetadata(true));

    public static readonly DependencyProperty CanDeleteProperty = DependencyProperty.Register(
        nameof(CanDelete),
        typeof(bool),
        typeof(CoachPackActionsMenu),
        new PropertyMetadata(false));

    public static readonly DependencyProperty DeleteActionLabelProperty = DependencyProperty.Register(
        nameof(DeleteActionLabel),
        typeof(string),
        typeof(CoachPackActionsMenu),
        new PropertyMetadata(string.Empty));

    public CoachPackActionsMenu()
    {
        InitializeComponent();
    }

    public event EventHandler? ExportRequested;

    public event EventHandler? DeleteRequested;

    public bool CanExport
    {
        get => (bool)GetValue(CanExportProperty);
        set => SetValue(CanExportProperty, value);
    }

    public bool CanDelete
    {
        get => (bool)GetValue(CanDeleteProperty);
        set => SetValue(CanDeleteProperty, value);
    }

    public string DeleteActionLabel
    {
        get => (string)GetValue(DeleteActionLabelProperty);
        set => SetValue(DeleteActionLabelProperty, value);
    }

    private void AdditionalActionsButton_Click(object sender, RoutedEventArgs e) =>
        ActionsPopup.IsOpen = true;

    private void ExportMenuButton_Click(object sender, RoutedEventArgs e)
    {
        ActionsPopup.IsOpen = false;
        ExportRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteMenuButton_Click(object sender, RoutedEventArgs e)
    {
        ActionsPopup.IsOpen = false;
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }
}
