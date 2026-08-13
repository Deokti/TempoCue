using TempoCue.Core;

namespace TempoCue.Coaching.Tests;

public sealed class MatchupPhaseSelectorTests
{
    private readonly MatchupPhaseSelector _selector = new();

    [Fact]
    public void Select_ReturnsSinglePhase_ForCoveredLevel()
    {
        var result = _selector.Select(
            [new MatchupPhaseCondition("early", 1, 8)],
            5);

        Assert.Equal(MatchupPhaseSelectionState.Selected, result.State);
        Assert.Equal("early", result.SelectedPhaseId);
    }

    [Fact]
    public void Select_ReturnsNotFound_ForUncoveredLevel()
    {
        var result = _selector.Select(
            [new MatchupPhaseCondition("early", 1, 8)],
            9);

        Assert.Equal(MatchupPhaseSelectionState.NotFound, result.State);
        Assert.Null(result.SelectedPhaseId);
    }

    [Fact]
    public void Select_PreservesInputOrder_ForAmbiguousLevel()
    {
        var result = _selector.Select(
            [
                new MatchupPhaseCondition("first", 1, 9),
                new MatchupPhaseCondition("second", 9, null)
            ],
            9);

        Assert.Equal(MatchupPhaseSelectionState.Ambiguous, result.State);
        Assert.Equal(["first", "second"], result.MatchingPhaseIds);
        Assert.Null(result.SelectedPhaseId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    public void Select_RejectsLevelOutsideGameRange(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _selector.Select([], level));
    }
}
