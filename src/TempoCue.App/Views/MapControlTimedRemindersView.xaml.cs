using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

public partial class MapControlTimedRemindersView : UserControl
{
    public MapControlTimedRemindersView() => InitializeComponent();

    private MapControlContentViewModel ViewModel =>
        (MapControlContentViewModel)DataContext;

    private void AddMapTimedReminderButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.TimedReminders.BeginAdd();

    private void IncreaseTimedReminderTimeButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.TimedReminders.Editor.IncreaseGameTime();

    private void DecreaseTimedReminderTimeButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.TimedReminders.Editor.DecreaseGameTime();

    private void MapTimedReminderGameTimeTextBox_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is TextBox { IsKeyboardFocusWithin: false } textBox)
        {
            e.Handled = true;
            textBox.Focus();
        }
    }

    private void MapTimedReminderGameTimeTextBox_GotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    private void MapTimedReminderGameTimeTextBox_PreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox
            || !e.Text.All(char.IsAsciiDigit)
            || CountResultingDigits(textBox, e.Text) > 4)
        {
            e.Handled = true;
        }
    }

    private void MapTimedReminderGameTimeTextBox_Pasting(
        object sender,
        DataObjectPastingEventArgs e)
    {
        var pastedText = e.DataObject.GetData(DataFormats.UnicodeText) as string;
        if (sender is not TextBox textBox
            || string.IsNullOrEmpty(pastedText)
            || !pastedText.All(char.IsAsciiDigit)
            || CountResultingDigits(textBox, pastedText) > 4)
        {
            e.CancelCommand();
        }
    }

    private void SaveMapTimedReminderButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.TimedReminders.SaveEditor();

    private void DuplicateEditedMapTimedReminderButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.TimedReminders.DuplicateEditor();

    private void DeleteEditedMapTimedReminderButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.TimedReminders.DeleteEditor();

    private void EditMapTimedReminderButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlTimedReminderViewModel reminder)
        {
            ViewModel.TimedReminders.BeginEdit(reminder);
        }
    }

    private void DuplicateMapTimedReminderButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlTimedReminderViewModel reminder)
        {
            ViewModel.TimedReminders.Duplicate(reminder);
        }
    }

    private void DeleteMapTimedReminderButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlTimedReminderViewModel reminder)
        {
            ViewModel.TimedReminders.Delete(reminder);
        }
    }

    private static int CountResultingDigits(TextBox textBox, string replacement)
    {
        var candidate = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, replacement);
        return candidate.Count(char.IsAsciiDigit);
    }
}
