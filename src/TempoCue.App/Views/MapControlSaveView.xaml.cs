using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;
using TempoCue.Coaching;

namespace TempoCue.App.Views;

/// <summary>Экран источника и итоговой карточки сохраняемого Map Control Pack.</summary>
public partial class MapControlSaveView : UserControl
{
    public MapControlSaveView()
    {
        InitializeComponent();
    }

    private MapControlSaveViewModel ViewModel => (MapControlSaveViewModel)DataContext;

    private void PersonalExperienceSourceButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedSourceKind = CoachPackSourceKind.PersonalExperience;

    private void ArticleSourceButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedSourceKind = CoachPackSourceKind.Article;

    private void VideoSourceButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedSourceKind = CoachPackSourceKind.Video;

    private void TrainingMaterialSourceButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedSourceKind = CoachPackSourceKind.CommunityGuide;
}
