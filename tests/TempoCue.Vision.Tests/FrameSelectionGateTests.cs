using System.Diagnostics;

namespace TempoCue.Vision.Tests;

public sealed class FrameSelectionGateTests
{
    [Fact]
    public void TrySelect_AllowsFirstFrameAndRejectsFrameBeforeInterval()
    {
        var gate = new FrameSelectionGate(30);

        Assert.True(gate.TrySelect(0));
        Assert.False(gate.TrySelect(Stopwatch.Frequency / 60));
    }

    [Fact]
    public void TrySelect_AllowsFrameAtNextInterval()
    {
        var gate = new FrameSelectionGate(30);
        var interval = (long)Math.Ceiling(Stopwatch.Frequency / 30d);

        Assert.True(gate.TrySelect(0));
        Assert.True(gate.TrySelect(interval));
    }

    [Fact]
    public void TrySelect_DoesNotAccumulateBurstAfterLongPause()
    {
        var gate = new FrameSelectionGate(30);
        var afterPause = Stopwatch.Frequency * 10;

        Assert.True(gate.TrySelect(0));
        Assert.True(gate.TrySelect(afterPause));
        Assert.False(gate.TrySelect(afterPause + 1));
    }

    [Fact]
    public void TrySelect_PreservesThirtyFpsCadenceForHundredFpsInput()
    {
        var gate = new FrameSelectionGate(30);
        var inputInterval = Stopwatch.Frequency / 100;
        var selectedCount = 0;

        for (var frame = 0; frame < 100; frame++)
        {
            if (gate.TrySelect(frame * inputInterval))
            {
                selectedCount++;
            }
        }

        Assert.InRange(selectedCount, 29, 31);
    }

    [Fact]
    public void Reset_AllowsNextFrameImmediately()
    {
        var gate = new FrameSelectionGate(30);
        Assert.True(gate.TrySelect(0));
        Assert.False(gate.TrySelect(1));

        gate.Reset();

        Assert.True(gate.TrySelect(1));
    }
}
