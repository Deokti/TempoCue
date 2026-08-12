using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Финальный экран сохранения локального Coach Pack.</summary>
public partial class MatchupSaveView : UserControl
{
    public MatchupSaveView()
    {
        InitializeComponent();
    }

    private MatchupSaveViewModel ViewModel => (MatchupSaveViewModel)DataContext;

    private void PersonalExperienceSourceButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedSourceKind = MatchupSourceKind.PersonalExperience;

    private void ArticleSourceButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedSourceKind = MatchupSourceKind.Article;

    private void VideoSourceButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedSourceKind = MatchupSourceKind.Video;

    private void GuideSourceButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedSourceKind = MatchupSourceKind.Guide;
}
