using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

public partial class MapControlVisionRulesView : UserControl
{
    public MapControlVisionRulesView() => InitializeComponent();

    private MapControlContentViewModel ViewModel =>
        (MapControlContentViewModel)DataContext;

    private void AddMapVisionRuleButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.VisionRules.BeginAdd();

    private void SaveMapVisionRuleButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.VisionRules.SaveEditor();

    private void DuplicateEditedMapVisionRuleButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.VisionRules.DuplicateEditor();

    private void DeleteEditedMapVisionRuleButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.VisionRules.DeleteEditor();

    private void EditMapVisionRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (Rule(sender) is { } rule)
        {
            ViewModel.VisionRules.BeginEdit(rule);
        }
    }

    private void DuplicateMapVisionRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (Rule(sender) is { } rule)
        {
            ViewModel.VisionRules.Duplicate(rule);
        }
    }

    private void DeleteMapVisionRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (Rule(sender) is { } rule)
        {
            ViewModel.VisionRules.Delete(rule);
        }
    }

    private void MoveMapVisionRuleUpButton_Click(object sender, RoutedEventArgs e) =>
        Move(sender, -1);

    private void MoveMapVisionRuleDownButton_Click(object sender, RoutedEventArgs e) =>
        Move(sender, 1);

    private void Move(object sender, int offset)
    {
        if (Rule(sender) is { } rule)
        {
            ViewModel.VisionRules.Move(rule, offset);
        }
    }

    private static MapControlVisionRuleViewModel? Rule(object sender) =>
        (sender as FrameworkElement)?.Tag as MapControlVisionRuleViewModel;
}
