using TempoCue.Core;

namespace TempoCue.Core.Tests;

public sealed class ChampionSelectStateTests
{
    [Fact]
    public void Participant_DoesNotExposeRiotIdForAnonymousSlot()
    {
        var participant = ChampionSelectParticipant.Anonymous(
            1,
            266,
            PlayerPosition.Top,
            isLocalPlayer: false);

        Assert.Equal(ChampionSelectIdentityVisibility.Anonymous, participant.IdentityVisibility);
        Assert.Null(participant.RiotId);
    }

    [Fact]
    public void Constructor_RejectsLocalCellMismatch()
    {
        var participants = new[]
        {
            ChampionSelectParticipant.Visible(
                0,
                6,
                PlayerPosition.Top,
                isLocalPlayer: true,
                RiotId.Create("Tempo Player", "TEST"))
        };

        Assert.Throws<ArgumentException>(() => new ChampionSelectState(1, participants));
    }

    [Fact]
    public void Constructor_CopiesParticipantCollection()
    {
        var participants = new List<ChampionSelectParticipant>
        {
            ChampionSelectParticipant.Unavailable(
                0,
                0,
                PlayerPosition.Unknown,
                isLocalPlayer: true)
        };
        var state = new ChampionSelectState(0, participants);

        participants.Clear();

        Assert.Single(state.Participants);
    }
}
