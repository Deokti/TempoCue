using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.Application.Tests;

/// <summary>
/// Прогоняет последовательности наблюдений через трекер, детектор переходов и
/// политику сигналов, проверяя приёмочные критерии уведомлений контроля карты:
/// количество ложных исчезновений и появлений, единственность настоящего
/// события и задержку его подтверждения. Кадры с ошибкой чтения по контракту
/// координатора не доходят до трекера и моделируются пропуском кадра во времени.
/// </summary>
public sealed class MapAwarenessSequenceTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MidVisibleWithRecognitionDips_ProducesNoSignals()
    {
        var harness = new SequenceHarness(CreateMatch());

        // Мид стоит на линии 10 секунд. Каждый третий кадр — слабый балл из-за
        // пинга и миньонов, дважды случаются полные пропуски распознавания на
        // один-два кадра, а один кадр вовсе потерян очередью latest-frame-wins.
        for (var frame = 0; frame < 40; frame++)
        {
            var at = Start.AddMilliseconds(frame * 250);
            if (frame is 14 or 27 or 28)
            {
                harness.Frame(at);
                continue;
            }

            if (frame == 33)
            {
                continue;
            }

            var confidence = frame % 3 == 0 ? 0.6 : 0.88;
            harness.Frame(at, new SequenceObservation("enemy-mid", confidence));
        }

        Assert.Empty(harness.Signals);
    }

    [Fact]
    public void MidLeavesLane_ProducesExactlyOneDisappearanceWithBoundedDelay()
    {
        var harness = new SequenceHarness(CreateMatch());
        var lastSeenAt = Start.AddMilliseconds(1750);
        for (var frame = 0; frame < 8; frame++)
        {
            harness.Frame(
                Start.AddMilliseconds(frame * 250),
                new SequenceObservation("enemy-mid", 0.88));
        }

        for (var frame = 8; frame < 40; frame++)
        {
            harness.Frame(Start.AddMilliseconds(frame * 250));
        }

        var signal = Assert.Single(harness.Signals);
        Assert.Equal(MapAwarenessSignalKind.EnemyMidDisappeared, signal.Kind);
        var delay = signal.OccurredAt - lastSeenAt;
        Assert.InRange(
            delay,
            ParticipantVisibilityTransitionDetector.DefaultMinimumAbsenceDuration,
            TimeSpan.FromSeconds(3.5));
    }

    [Fact]
    public void MidDies_ProducesNoDisappearanceSignal()
    {
        var match = CreateMatch();
        var harness = new SequenceHarness(match);
        for (var frame = 0; frame < 8; frame++)
        {
            harness.Frame(
                Start.AddMilliseconds(frame * 250),
                new SequenceObservation("enemy-mid", 0.88));
        }

        // Live Client Data пометил мидера мёртвым: он исключается из ожидаемых,
        // а политика дополнительно отклоняет мёртвых участников.
        harness.Match = CreateMatch(midIsDead: true);
        for (var frame = 8; frame < 40; frame++)
        {
            harness.Frame(Start.AddMilliseconds(frame * 250));
        }

        Assert.Empty(harness.Signals);
    }

    [Fact]
    public void StreamOutage_ProducesNoSignalsOnDegradeOrRecovery()
    {
        var harness = new SequenceHarness(CreateMatch());
        for (var frame = 0; frame < 8; frame++)
        {
            harness.Frame(
                Start.AddMilliseconds(frame * 250),
                new SequenceObservation("enemy-mid", 0.88));
        }

        // Поток кадров устарел: координатор деградирует состояния в «нет данных».
        harness.StaleDegrade(Start.AddSeconds(4));

        // Восстановление потока само по себе не рождает событий — ни для
        // по-прежнему видимого мида, ни для отсутствующего лесника.
        for (var frame = 0; frame < 12; frame++)
        {
            harness.Frame(
                Start.AddSeconds(5).AddMilliseconds(frame * 250),
                new SequenceObservation("enemy-mid", 0.88));
        }

        Assert.Empty(harness.Signals);
    }

    [Fact]
    public void JunglerAppears_SignalsOnSecondStrongFrameWithoutRegression()
    {
        var harness = new SequenceHarness(CreateMatch());
        for (var frame = 0; frame < 8; frame++)
        {
            harness.Frame(Start.AddMilliseconds(frame * 250));
        }

        var firstSightingAt = Start.AddMilliseconds(8 * 250);
        harness.Frame(firstSightingAt, new SequenceObservation("enemy-jungle", 0.9));
        harness.Frame(
            firstSightingAt.AddMilliseconds(250),
            new SequenceObservation("enemy-jungle", 0.9));

        var signal = Assert.Single(harness.Signals);
        Assert.Equal(MapAwarenessSignalKind.EnemyJunglerAppeared, signal.Kind);
        // Задержка появления не деградировала: сигнал на втором сильном кадре.
        Assert.Equal(firstSightingAt.AddMilliseconds(250), signal.OccurredAt);
    }

    private static MatchState CreateMatch(bool midIsDead = false) => new()
    {
        GameTimeSeconds = 600,
        ActivePlayerId = "active-player",
        Players =
        [
            new PlayerState
            {
                ParticipantId = "active-player",
                ChampionName = "Pyke",
                Team = TeamSide.Order,
                Position = PlayerPosition.Support,
                Level = 6
            },
            new PlayerState
            {
                ParticipantId = "enemy-mid",
                ChampionName = "Anivia",
                Team = TeamSide.Chaos,
                Position = PlayerPosition.Middle,
                Level = 6,
                IsDead = midIsDead
            },
            new PlayerState
            {
                ParticipantId = "enemy-jungle",
                ChampionName = "Amumu",
                Team = TeamSide.Chaos,
                Position = PlayerPosition.Jungle,
                Level = 6
            }
        ]
    };

    private sealed record SequenceObservation(
        string ParticipantId,
        double Confidence,
        double MapX = 0.45,
        double MapY = 0.55);

    private sealed class SequenceHarness(MatchState match)
    {
        private readonly ParticipantVisibilityTracker _tracker = new();
        private readonly ParticipantVisibilityTransitionDetector _detector = new();

        public MatchState Match { get; set; } = match;

        public List<MapAwarenessSignal> Signals { get; } = [];

        public void Frame(DateTimeOffset capturedAt, params SequenceObservation[] seen)
        {
            var states = _tracker.Update(
                capturedAt,
                ExpectedParticipants(),
                seen
                    .Select(observation => new ParticipantVisionObservation(
                        observation.ParticipantId,
                        observation.Confidence,
                        observation.MapX,
                        observation.MapY))
                    .ToArray());
            foreach (var transition in _detector.Update(capturedAt, states))
            {
                if (MapAwarenessSignalPolicy.Evaluate(transition, Match) is { } signal)
                {
                    Signals.Add(signal);
                }
            }
        }

        /// <summary>Повторяет деградацию координатора при устаревшем потоке кадров.</summary>
        public void StaleDegrade(DateTimeOffset at)
        {
            var states = _tracker.Update(at, ExpectedParticipants(), []);
            var unknownStates = states
                .Select(static state => state with
                {
                    Status = ParticipantVisibilityStatus.Unknown
                })
                .ToArray();
            foreach (var transition in _detector.Update(at, unknownStates))
            {
                if (MapAwarenessSignalPolicy.Evaluate(transition, Match) is { } signal)
                {
                    Signals.Add(signal);
                }
            }
        }

        private string[] ExpectedParticipants() => Match.Players
            .Where(static player => !player.IsDead)
            .Select(static player => player.ParticipantId)
            .ToArray();
    }
}
