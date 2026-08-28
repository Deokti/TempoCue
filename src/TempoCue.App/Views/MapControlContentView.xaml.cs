using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

public partial class MapControlContentView : UserControl
{
    public MapControlContentView() => InitializeComponent();

    private MapControlContentViewModel ViewModel =>
        (MapControlContentViewModel)DataContext;

    private void MapObjectsTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowObjects();

    private void MapVisionRulesTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowVisionRules();

    private void MapTimedRemindersTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowTimedReminders();

    private void MapPersistentTipsTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowPersistentTips();

    private void AddMapObjectiveRuleButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenObjectivePicker();

    private void CloseMapObjectivePickerButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.CloseObjectivePicker();

    private void SelectMapObjectiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlObjectiveOptionViewModel option)
        {
            ViewModel.BeginAddRule(option.Objective);
        }
    }

    private void IncreaseLeadTimeButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Editor.IncreaseLeadTime();

    private void DecreaseLeadTimeButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Editor.DecreaseLeadTime();

    private void SaveMapRuleButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SaveEditor();

    private void CancelMapRuleButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.CancelEditor();

    private void DuplicateEditedMapRuleButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DuplicateEditor();

    private void DeleteEditedMapRuleButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DeleteEditor();

    private void EditMapRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlObjectiveRuleViewModel rule)
        {
            ViewModel.BeginEditRule(rule);
        }
    }

    private void DuplicateMapRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlObjectiveRuleViewModel rule)
        {
            ViewModel.DuplicateRule(rule);
        }
    }

    private void DeleteMapRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlObjectiveRuleViewModel rule)
        {
            ViewModel.DeleteRule(rule);
        }
    }

    private void MoveMapRuleUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlObjectiveRuleViewModel rule)
        {
            ViewModel.MoveRule(rule, -1);
        }
    }

    private void MoveMapRuleDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlObjectiveRuleViewModel rule)
        {
            ViewModel.MoveRule(rule, 1);
        }
    }
}
