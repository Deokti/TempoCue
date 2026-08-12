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
