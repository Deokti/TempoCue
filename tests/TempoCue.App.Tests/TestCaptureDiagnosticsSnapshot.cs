using TempoCue.Vision;

namespace TempoCue.App.Tests;

/// <summary>Создаёт диагностические снимки без хрупких позиционных аргументов в UI-тестах.</summary>
internal static class TestCaptureDiagnosticsSnapshot
{
    public static CaptureDiagnosticsSnapshot Create(
        CaptureSessionState state = CaptureSessionState.Idle,
        string? targetName = null,
        int frameWidth = 0,
        int frameHeight = 0,
        long receivedFrameCount = 0,
        double framesPerSecond = 0,
        int selectionFrameRateLimit = 30,
        long selectedFrameCount = 0,
        double selectionFramesPerSecond = 0,
        long rateLimitedFrameCount = 0,
        DateTimeOffset? lastFrameAt = null,
        MinimapPreviewSnapshot? minimapPreview = null,
        string? minimapPreviewErrorMessage = null,
        string? errorMessage = null) =>
        new(
            state,
            targetName,
            frameWidth,
            frameHeight,
            receivedFrameCount,
            framesPerSecond,
            selectionFrameRateLimit,
            selectedFrameCount,
            selectionFramesPerSecond,
            rateLimitedFrameCount,
            lastFrameAt,
            minimapPreview,
            minimapPreviewErrorMessage,
            errorMessage);
}
