using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TempoCue.Overlay;

/// <summary>Отображает один слот и владеет только его переходами появления и исчезновения.</summary>
internal partial class OverlayCardView : UserControl
{
    private readonly TranslateTransform _translation = new();
    private long _animationVersion;
    private string? _notificationKey;

    public OverlayCardView()
    {
        InitializeComponent();
        RenderTransform = _translation;
        Visibility = Visibility.Collapsed;
    }

    public event EventHandler? DismissRequested;

    public event EventHandler? Hidden;

    public bool IsInteractive
    {
        set => CloseOverlayButton.Visibility = value
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public void SetBackgroundOpacity(double opacity)
    {
        var normalizedOpacity = Math.Clamp(opacity, 0, 1);
        OverlayCard.Background = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(byte.MaxValue * normalizedOpacity),
            0x10,
            0x18,
            0x27));
    }

    public void ShowNotification(
        OverlayNotification notification,
        OverlayDisplayPolicy policy,
        bool reducedMotion)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(policy);

        var updatesExistingCard = Visibility is Visibility.Visible &&
            string.Equals(_notificationKey, notification.Key, StringComparison.Ordinal);
        _notificationKey = notification.Key;
        DataContext = notification;
        if (updatesExistingCard)
        {
            return;
        }

        var version = ++_animationVersion;
        Visibility = Visibility.Visible;
        BeginAnimation(OpacityProperty, null);
        _translation.BeginAnimation(TranslateTransform.XProperty, null);
        Opacity = 0;
        _translation.X = reducedMotion ? 0 : 8;

        var duration = notification.Tone is OverlayNotificationTone.Danger
            ? policy.UrgentEnterDuration
            : policy.EnterDuration;
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        });
        if (!reducedMotion)
        {
            var movement = new DoubleAnimation(8, 0, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            };
            movement.Completed += (_, _) =>
            {
                if (version == _animationVersion)
                {
                    _translation.X = 0;
                }
            };
            _translation.BeginAnimation(TranslateTransform.XProperty, movement);
        }

        Opacity = 1;
    }

    public void HideCard(OverlayDisplayPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (Visibility is not Visibility.Visible)
        {
            return;
        }

        var version = ++_animationVersion;
        var animation = new DoubleAnimation(Opacity, 0, policy.ExitDuration)
        {
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            if (version != _animationVersion)
            {
                return;
            }

            Opacity = 0;
            Visibility = Visibility.Collapsed;
            _notificationKey = null;
            DataContext = null;
            Hidden?.Invoke(this, EventArgs.Empty);
        };
        BeginAnimation(OpacityProperty, animation);
    }

    private void CloseOverlayButton_Click(object sender, RoutedEventArgs e) =>
        DismissRequested?.Invoke(this, EventArgs.Empty);
}
