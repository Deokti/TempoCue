using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TempoCue.App;

public partial class MainWindow
{
    private const int OverlaySetupHotKeyId = 0x5443;
    private const int HotKeyMessage = 0x0312;
    private const uint AltModifier = 0x0001;
    private const uint ControlModifier = 0x0002;
    private const uint OVirtualKey = 0x4F;

    private HwndSource? _hotKeySource;
    private bool _isOverlayHotKeyRegistered;

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hotKeySource = HwndSource.FromHwnd(handle);
        _hotKeySource?.AddHook(ProcessWindowMessage);
        _isOverlayHotKeyRegistered = RegisterHotKey(
            handle,
            OverlaySetupHotKeyId,
            AltModifier | ControlModifier,
            OVirtualKey);
        Closed += MainWindow_OverlayHotKeyClosed;
    }

    private IntPtr ProcessWindowMessage(
        IntPtr window,
        int message,
        IntPtr wordParameter,
        IntPtr longParameter,
        ref bool handled)
    {
        if (message == HotKeyMessage && wordParameter.ToInt32() == OverlaySetupHotKeyId)
        {
            _settingsViewModel.OpenOverlaySetup();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void MainWindow_OverlayHotKeyClosed(object? sender, EventArgs e)
    {
        if (_hotKeySource is null)
        {
            return;
        }

        if (_isOverlayHotKeyRegistered)
        {
            _ = UnregisterHotKey(_hotKeySource.Handle, OverlaySetupHotKeyId);
        }

        _hotKeySource.RemoveHook(ProcessWindowMessage);
        _hotKeySource = null;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
