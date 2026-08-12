using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace TempoCue.Overlay;

/// <summary>Окно двух слотов, не интерпретирующее игровые события.</summary>
internal partial class OverlayWindow : Window
{
    private const int ExtendedStyleIndex = -20;
    private const long TransparentStyle = 0x00000020L;
    private const long ToolWindowStyle = 0x00000080L;
    private const long NoActivateStyle = 0x08000000L;

    private readonly OverlayDisplayPolicy _policy;
    private OverlayInteractionMode _interactionMode;

    public OverlayWindow(OverlayDisplayPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        InitializeComponent();
        PrimaryCard.DismissRequested += Card_DismissRequested;
        SecondaryCard.DismissRequested += Card_DismissRequested;
        PrimaryCard.Hidden += Card_Hidden;
        SecondaryCard.Hidden += Card_Hidden;
        SourceInitialized += OverlayWindow_SourceInitialized;
    }

    public event EventHandler? HideRequested;

    public event EventHandler? AllCardsHidden;

    internal OverlayCardView PrimaryCardView => PrimaryCard;

    internal OverlayCardView SecondaryCardView => SecondaryCard;

    public void SetInteractionMode(OverlayInteractionMode mode)
    {
        _interactionMode = mode;
        var isInteractive = mode is OverlayInteractionMode.Setup;
        PrimaryCard.IsInteractive = isInteractive;
        SecondaryCard.IsInteractive = isInteractive;
        ApplyExtendedStyles();
    }

    public void ApplySettings(OverlayPresentationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();
        Width = normalized.Width;
        PrimaryCard.SetBackgroundOpacity(normalized.BackgroundOpacity);
        SecondaryCard.SetBackgroundOpacity(normalized.BackgroundOpacity);
    }

    public void ShowNotifications(OverlayScheduleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var reducedMotion = !SystemParameters.ClientAreaAnimation;
        ApplySlot(PrimaryCard, snapshot.Items.ElementAtOrDefault(0), reducedMotion);
        ApplySlot(SecondaryCard, snapshot.Items.ElementAtOrDefault(1), reducedMotion);
    }

    public void HideNotifications()
    {
        PrimaryCard.HideCard(_policy);
        SecondaryCard.HideCard(_policy);
    }

    private void ApplySlot(
        OverlayCardView card,
        OverlayDisplayItem? item,
        bool reducedMotion)
    {
        if (item is null)
        {
            card.HideCard(_policy);
            return;
        }

        card.ShowNotification(item.Notification, _policy, reducedMotion);
    }

    private void OverlayCardsPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_interactionMode is OverlayInteractionMode.Setup &&
            e.ChangedButton is MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OverlayWindow_SourceInitialized(object? sender, EventArgs e) =>
        ApplyExtendedStyles();

    private void ApplyExtendedStyles()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var styles = GetWindowLongPtr(handle, ExtendedStyleIndex).ToInt64();
        styles |= ToolWindowStyle | NoActivateStyle;
        if (_interactionMode is OverlayInteractionMode.Game)
        {
            styles |= TransparentStyle;
        }
        else
        {
            styles &= ~TransparentStyle;
        }

        _ = SetWindowLongPtr(handle, ExtendedStyleIndex, new IntPtr(styles));
    }

    private void Card_DismissRequested(object? sender, EventArgs e) =>
        HideRequested?.Invoke(this, EventArgs.Empty);

    private void Card_Hidden(object? sender, EventArgs e)
    {
        if (PrimaryCard.Visibility is Visibility.Collapsed &&
            SecondaryCard.Visibility is Visibility.Collapsed)
        {
            AllCardsHidden?.Invoke(this, EventArgs.Empty);
        }
    }

    private static IntPtr GetWindowLongPtr(IntPtr window, int index) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(window, index)
            : new IntPtr(GetWindowLong32(window, index));

    private static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(window, index, value)
            : new IntPtr(SetWindowLong32(window, index, value.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr window, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);
}
