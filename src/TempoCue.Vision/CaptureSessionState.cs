namespace TempoCue.Vision;

public enum CaptureSessionState
{
    Idle,
    SelectingTarget,
    Capturing,
    Stopped,
    Failed,
    Unsupported
}
