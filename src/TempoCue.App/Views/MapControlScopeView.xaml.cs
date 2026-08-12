using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Шаг выбора ролей и стороны команды для набора контроля карты.</summary>
public partial class MapControlScopeView : UserControl
{
    public MapControlScopeView()
    {
        InitializeComponent();
    }

    private void AllMapControlRolesCheckBox_Click(object sender, RoutedEventArgs e) =>
        ((MapControlScopeViewModel)DataContext).SelectAllRoles();
}
