using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TempoCue.Vision;

/// <summary>Находит только главное окно запущенного матча, не затрагивая Riot Client.</summary>
internal static class LeagueWindowCaptureTarget
{
    internal const string ProcessName = "League of Legends";
    private const string ExpectedWindowTitle = "League of Legends (TM) Client";

    public static nint FindWindowHandle()
    {
        var windowHandle = FindWindow(null, ExpectedWindowTitle);
        if (windowHandle == nint.Zero)
        {
            return nint.Zero;
        }

        _ = GetWindowThreadProcessId(windowHandle, out var processId);
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return Select([
                new LeagueWindowCandidate(
                    windowHandle,
                    process.ProcessName,
                    ExpectedWindowTitle)
            ])?.WindowHandle ?? nint.Zero;
        }
        catch (ArgumentException)
        {
            return nint.Zero;
        }
        catch (InvalidOperationException)
        {
            return nint.Zero;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return nint.Zero;
        }
    }

    internal static LeagueWindowCandidate? Select(
        IEnumerable<LeagueWindowCandidate> candidates) =>
        candidates.FirstOrDefault(static candidate =>
            candidate.WindowHandle != nint.Zero &&
            string.Equals(
                candidate.ProcessName,
                ProcessName,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                candidate.WindowTitle,
                ExpectedWindowTitle,
                StringComparison.OrdinalIgnoreCase));

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindow(string? className, string windowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}

internal sealed record LeagueWindowCandidate(
    nint WindowHandle,
    string ProcessName,
    string WindowTitle);
