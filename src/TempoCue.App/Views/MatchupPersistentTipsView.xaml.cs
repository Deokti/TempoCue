using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Редактор и список постоянных подсказок матчапа.</summary>
public partial class MatchupPersistentTipsView : UserControl
{
    public MatchupPersistentTipsView()
    {
        InitializeComponent();
    }

    private MatchupPersistentTipsViewModel ViewModel =>
        ((MatchupContentViewModel)DataContext).PersistentTips;

    private void AddPersistentTipButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.BeginAdd();

    private void SavePersistentTipButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SaveEditor();

    private void DuplicateEditedPersistentTipButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DuplicateEditor();

    private void DeleteEditedPersistentTipButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DeleteEditor();

    private void EditPersistentTipButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.BeginEdit(GetTip(sender));

    private void DuplicatePersistentTipButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Duplicate(GetTip(sender));

    private void DeletePersistentTipButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Delete(GetTip(sender));

    private static MatchupPersistentTipViewModel GetTip(object sender) =>
        (MatchupPersistentTipViewModel)((FrameworkElement)sender).DataContext;
}
