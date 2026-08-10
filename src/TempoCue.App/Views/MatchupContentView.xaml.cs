using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Фазы и остальные разделы содержания матчапа.</summary>
public partial class MatchupContentView : UserControl
{
    public MatchupContentView()
    {
        InitializeComponent();
        IsVisibleChanged += MatchupContentView_IsVisibleChanged;
    }

    private MatchupContentViewModel ViewModel =>
        (MatchupContentViewModel)DataContext;

    private MatchupPhaseEditorViewModel Editor => ViewModel.Editor;

    // Каталог загружается при входе на шаг, но ошибка не мешает создавать фазу без предметов.
    private async void MatchupContentView_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && DataContext is MatchupContentViewModel viewModel)
        {
            await viewModel.EnsureItemCatalogLoadedAsync();
        }
    }

    private async void RetryItemCatalogButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.RetryItemCatalogLoadAsync();

    private void SelectStartItemButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenStartItemPicker();

    private void SelectEndItemButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenEndItemPicker();

    private void RemoveStartItemButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RemoveStartItem();

    private void RemoveEndItemButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RemoveEndItem();

    // Действия с открытым редактором новой или выбранной фазы.
    private void AddPhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.BeginAddPhase();

    private void SavePhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SaveEditor();

    private void DuplicateEditedPhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DuplicateEditor();

    private void DeleteEditedPhaseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DeleteEditor();

    // Действия над сохранённой карточкой фазы.
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

    // Кнопки диапазона изменяют значения в редакторе, а не в сохранённой фазе напрямую.
    private void DecreaseMinimumLevelButton_Click(object sender, RoutedEventArgs e) =>
        Editor.DecreaseMinimumLevel();

    private void IncreaseMinimumLevelButton_Click(object sender, RoutedEventArgs e) =>
        Editor.IncreaseMinimumLevel();

    private void DecreaseMaximumLevelButton_Click(object sender, RoutedEventArgs e) =>
        Editor.DecreaseMaximumLevel();

    private void IncreaseMaximumLevelButton_Click(object sender, RoutedEventArgs e) =>
        Editor.IncreaseMaximumLevel();

    private static MatchupPhaseViewModel GetPhase(object sender) =>
        (MatchupPhaseViewModel)((FrameworkElement)sender).DataContext;
}
