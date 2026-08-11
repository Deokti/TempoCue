using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.Views;

namespace TempoCue.App.Tests;

/// <summary>Общие действия для независимых сценариев раздела коучинга.</summary>
internal static class CoachingViewTestContext
{
    public static CoachingLibraryView OpenLibrary(MainWindowTestContext context)
    {
        context.Show();
        WpfTestHost.Click(WpfTestHost.Find<Button>(context.Window, "CoachingNavigationButton"));
        context.Coaching.Library.EnsureLoadedAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        return context.CoachingLibraryPage;
    }

    public static void SaveVisualSnapshotIfRequested(Window window, string fileName)
    {
        var directory = Environment.GetEnvironmentVariable("TEMPOCUE_UI_SNAPSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            WpfTestHost.SavePng(window, Path.Combine(directory, fileName));
        }
    }
}
