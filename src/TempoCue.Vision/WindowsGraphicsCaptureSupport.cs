using Windows.Graphics.Capture;

namespace TempoCue.Vision;

/// <summary>
/// Проверяет наличие системного механизма захвата до создания долгоживущей сессии.
/// </summary>
public sealed class WindowsGraphicsCaptureSupport
{
    public bool IsSupported => GraphicsCaptureSession.IsSupported();
}
