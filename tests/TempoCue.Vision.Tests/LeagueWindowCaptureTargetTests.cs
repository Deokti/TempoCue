using TempoCue.Vision;

namespace TempoCue.Vision.Tests;

public sealed class LeagueWindowCaptureTargetTests
{
    [Fact]
    public void Select_ReturnsOnlyExactMatchWindow()
    {
        var candidates = new[]
        {
            new LeagueWindowCandidate(11, "League of Legends", "League of Legends"),
            new LeagueWindowCandidate(22, "League of Legends", "League of Legends (TM) Client"),
            new LeagueWindowCandidate(33, "LeagueClientUx", "League of Legends (TM) Client")
        };

        var selected = LeagueWindowCaptureTarget.Select(candidates);

        Assert.NotNull(selected);
        Assert.Equal((nint)22, selected.WindowHandle);
    }

    [Fact]
    public void Select_RejectsMissingHandleAndUnrelatedWindows()
    {
        var candidates = new[]
        {
            new LeagueWindowCandidate(0, "League of Legends", "League of Legends (TM) Client"),
            new LeagueWindowCandidate(44, "TempoCue", "TempoCue")
        };

        Assert.Null(LeagueWindowCaptureTarget.Select(candidates));
    }
}
