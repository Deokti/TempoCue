using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Список сохранённых фаз и действия над их порядком.</summary>
public partial class MatchupPhaseListView : UserControl
{
    public MatchupPhaseListView()
    {
        InitializeComponent();
    }

    private MatchupContentViewModel ViewModel => (MatchupContentViewModel)DataContext;

    private void AddPhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.BeginAddPhase();

    private void EditPhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.BeginEditPhase(GetPhase(sender));

    private void DuplicatePhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DuplicatePhase(GetPhase(sender));

    private void DeletePhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DeletePhase(GetPhase(sender));

    private void MovePhaseUpButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.MovePhaseUp(GetPhase(sender));

    private void MovePhaseDownButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.MovePhaseDown(GetPhase(sender));

    private static MatchupPhaseViewModel GetPhase(object sender) =>
        (MatchupPhaseViewModel)((FrameworkElement)sender).DataContext;
}
