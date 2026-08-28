using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

public partial class MapControlVerificationView : UserControl
{
    public MapControlVerificationView() => InitializeComponent();

    private void ShowVisionCheckNotificationButton_Click(object sender, RoutedEventArgs e) =>
        (DataContext as MapControlVerificationViewModel)?.ShowCheckedNotification();
}
