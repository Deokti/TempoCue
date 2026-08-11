using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TempoCue.App.Views;

/// <summary>Модальное подтверждение импорта проверенного Coach Pack.</summary>
public partial class CoachPackImportDialogView : UserControl
{
    public CoachPackImportDialogView()
    {
        InitializeComponent();
    }

    public event EventHandler? CloseRequested;

    public event EventHandler? ConfirmRequested;

    private void CloseImportDialogButton_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void CancelImportButton_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void ConfirmImportButton_Click(object sender, RoutedEventArgs e) =>
        ConfirmRequested?.Invoke(this, EventArgs.Empty);

    private void CoachPackImportDialogView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void CoachPackImportDialogView_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () => Keyboard.Focus(CancelImportButton),
            DispatcherPriority.Input);
    }
}
