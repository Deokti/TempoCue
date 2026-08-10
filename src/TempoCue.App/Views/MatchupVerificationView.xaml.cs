using System.Windows;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

public partial class MatchupVerificationView
{
    public MatchupVerificationView()
    {
        InitializeComponent();
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MatchupVerificationViewModel viewModel
            && sender is FrameworkElement
            {
                DataContext: MatchupVerificationPresetViewModel preset
            })
        {
            viewModel.SelectPreset(preset);
        }
    }

    private void DecreaseLevelButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MatchupVerificationViewModel viewModel)
        {
            viewModel.DecreaseLevel();
        }
    }

    private void IncreaseLevelButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MatchupVerificationViewModel viewModel)
        {
            viewModel.IncreaseLevel();
        }
    }
}
