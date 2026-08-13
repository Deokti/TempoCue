using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TempoCue.App.Controls;

namespace TempoCue.App.Tests;

public sealed class ScrollViewerWheelRoutingTests
{
    [Fact]
    public void MouseWheelOverNonScrollableList_ContinuesScrollingOuterPage()
    {
        WpfTestHost.Run(() =>
        {
            var listBox = new ListBox
            {
                Height = 80,
                ItemsSource = new[] { "Первый", "Второй" }
            };
            var content = new StackPanel();
            content.Children.Add(new Border { Height = 180 });
            content.Children.Add(listBox);
            content.Children.Add(new Border { Height = 500 });

            var outer = new ScrollViewer
            {
                Width = 320,
                Height = 180,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = content
            };
            ScrollViewerWheelRouting.SetIsEnabled(outer, true);

            var window = new Window
            {
                Width = 360,
                Height = 240,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = outer
            };

            try
            {
                window.Show();
                WpfTestHost.Pump();
                outer.ScrollToVerticalOffset(120);
                WpfTestHost.Pump();
                var initialOffset = outer.VerticalOffset;

                var firstItem = Assert.IsType<ListBoxItem>(
                    listBox.ItemContainerGenerator.ContainerFromIndex(0));

                firstItem.RaiseEvent(new MouseWheelEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    -120)
                {
                    RoutedEvent = Mouse.PreviewMouseWheelEvent,
                    Source = firstItem
                });
                outer.UpdateLayout();
                WpfTestHost.Pump(System.Windows.Threading.DispatcherPriority.Render);

                Assert.True(outer.VerticalOffset > initialOffset);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MouseWheelOverScrollableNestedList_DoesNotMoveOuterPage()
    {
        WpfTestHost.Run(() =>
        {
            var listBox = new ListBox
            {
                Height = 80,
                ItemsSource = Enumerable.Range(1, 20).Select(index => $"Item {index}").ToArray()
            };
            var content = new StackPanel();
            content.Children.Add(new Border { Height = 180 });
            content.Children.Add(listBox);
            content.Children.Add(new Border { Height = 500 });

            var outer = new ScrollViewer
            {
                Width = 320,
                Height = 180,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = content
            };
            ScrollViewerWheelRouting.SetIsEnabled(outer, true);

            var window = new Window
            {
                Width = 360,
                Height = 240,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = outer
            };

            try
            {
                window.Show();
                WpfTestHost.Pump();
                outer.ScrollToVerticalOffset(120);
                outer.UpdateLayout();
                WpfTestHost.Pump(System.Windows.Threading.DispatcherPriority.Render);
                var initialOffset = outer.VerticalOffset;
                var firstItem = Assert.IsType<ListBoxItem>(
                    listBox.ItemContainerGenerator.ContainerFromIndex(0));

                firstItem.RaiseEvent(new MouseWheelEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    -120)
                {
                    RoutedEvent = Mouse.PreviewMouseWheelEvent,
                    Source = firstItem
                });
                outer.UpdateLayout();
                WpfTestHost.Pump(System.Windows.Threading.DispatcherPriority.Render);

                Assert.Equal(initialOffset, outer.VerticalOffset);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
