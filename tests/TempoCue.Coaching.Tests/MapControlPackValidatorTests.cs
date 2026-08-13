using TempoCue.Core;

namespace TempoCue.Coaching.Tests;

public sealed class MapControlPackValidatorTests
{
    [Fact]
    public void Validate_AcceptsFuturePatchUsingLatestVerifiedTimings()
    {
        var pack = MapControlPackTestData.Create() with { Patch = "16.16" };

        MapControlPackValidator.Validate(pack);
    }

    [Fact]
    public void Validate_AcceptsPackWithOnlyTimedReminder()
    {
        var original = MapControlPackTestData.Create();
        var pack = original with
        {
            Rules = [],
            PersistentTips = []
        };

        MapControlPackValidator.Validate(pack);
    }

    [Fact]
    public void Validate_AcceptsPackWithOnlyPersistentTip()
    {
        var original = MapControlPackTestData.Create();
        var pack = original with
        {
            Rules = [],
            TimedReminders = []
        };

        MapControlPackValidator.Validate(pack);
    }

    [Fact]
    public void Validate_RejectsPackWithoutContent()
    {
        var pack = MapControlPackTestData.Create() with
        {
            Rules = [],
            TimedReminders = [],
            PersistentTips = []
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => MapControlPackValidator.Validate(pack));

        Assert.Contains("at least one", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsUnsupportedPackType()
    {
        var pack = MapControlPackTestData.Create() with { PackType = "matchup" };

        var exception = Assert.Throws<InvalidDataException>(
            () => MapControlPackValidator.Validate(pack));

        Assert.Contains("type", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsDuplicateRuleIds()
    {
        var original = MapControlPackTestData.Create();
        var duplicate = original.Rules[1] with { Id = original.Rules[0].Id };
        var pack = original with { Rules = [original.Rules[0], duplicate] };

        var exception = Assert.Throws<InvalidDataException>(
            () => MapControlPackValidator.Validate(pack));

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsObjectiveScopeWithoutObjective()
    {
        var original = MapControlPackTestData.Create();
        var invalidTip = original.PersistentTips[0] with
        {
            Scope = new MapControlPersistentTipScope(
                MapControlPersistentTipScopeKind.SpecificObjective)
        };
        var pack = original with { PersistentTips = [invalidTip] };

        var exception = Assert.Throws<InvalidDataException>(
            () => MapControlPackValidator.Validate(pack));

        Assert.Contains("objective", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsTimedReminderOutsideFifteenSecondStep()
    {
        var original = MapControlPackTestData.Create();
        var invalidReminder = original.TimedReminders[0] with
        {
            GameTime = TimeSpan.FromSeconds(17)
        };
        var pack = original with { TimedReminders = [invalidReminder] };

        var exception = Assert.Throws<InvalidDataException>(
            () => MapControlPackValidator.Validate(pack));

        Assert.Contains("15-second", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsPackWithoutRequiredMetadata()
    {
        var pack = MapControlPackTestData.Create() with { Description = string.Empty };

        var exception = Assert.Throws<InvalidDataException>(
            () => MapControlPackValidator.Validate(pack));

        Assert.Contains("metadata", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsPackWithoutApplicableRoles()
    {
        var pack = MapControlPackTestData.Create() with
        {
            Applicability = new MapControlApplicability { Roles = [] }
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => MapControlPackValidator.Validate(pack));

        Assert.Contains("role", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AcceptsExternalSourceWithoutLink()
    {
        var pack = MapControlPackTestData.Create() with
        {
            Source = new CoachPackSource
            {
                Kind = CoachPackSourceKind.Article,
                Title = "Map control guide",
                Author = "Guide author",
                Url = string.Empty,
                Permission = CoachPackSourcePermission.NotConfirmed
            }
        };

        MapControlPackValidator.Validate(pack);
    }

    [Fact]
    public void Validate_RejectsExternalSourceWithNonHttpLink()
    {
        var pack = MapControlPackTestData.Create() with
        {
            Source = new CoachPackSource
            {
                Kind = CoachPackSourceKind.Article,
                Title = "Map control guide",
                Author = "Guide author",
                Url = "file:///guide.html",
                Permission = CoachPackSourcePermission.NotConfirmed
            }
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => MapControlPackValidator.Validate(pack));

        Assert.Contains("HTTP", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsRespawnRuleForOneTimeObjective()
    {
        var original = MapControlPackTestData.Create();
        var invalidRule = original.Rules[1] with
        {
            Objective = MapObjectiveKind.VoidGrubs
        };
        var pack = original with { Rules = [invalidRule] };

        var exception = Assert.Throws<InvalidDataException>(
            () => MapControlPackValidator.Validate(pack));

        Assert.Contains("does not respawn", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Validate_RejectsUnsupportedSchema(int schemaVersion)
    {
        var pack = MapControlPackTestData.Create() with { SchemaVersion = schemaVersion };

        Assert.Throws<InvalidDataException>(() => MapControlPackValidator.Validate(pack));
    }
}
