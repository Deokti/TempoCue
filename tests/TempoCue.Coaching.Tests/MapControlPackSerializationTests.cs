using System.Text;
using System.Text.Json;

namespace TempoCue.Coaching.Tests;

public sealed class MapControlPackSerializationTests
{
    [Fact]
    public async Task WriteAndLoad_RoundTripsMapControlContent()
    {
        var pack = MapControlPackTestData.Create();

        await using var stream = new MemoryStream();
        await MapControlPackWriter.WriteAsync(stream, pack);
        stream.Position = 0;

        var loaded = MapControlPackLoader.Load(stream);

        Assert.Equal(MapControlPack.SupportedPackType, loaded.PackType);
        Assert.Equal(pack.Id, loaded.Id);
        Assert.Equal(pack.Source, loaded.Source);
        Assert.Equal(pack.Applicability.Roles, loaded.Applicability.Roles);
        Assert.Equal(pack.Applicability.TeamSide, loaded.Applicability.TeamSide);
        Assert.Equal(pack.Rules, loaded.Rules);
        Assert.Equal(pack.TimedReminders, loaded.TimedReminders);
        Assert.Equal(pack.PersistentTips, loaded.PersistentTips);
    }

    [Fact]
    public void Load_RejectsFileWithoutPackTypeDiscriminator()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            """
            {
              "schemaVersion": 1,
              "id": "map-control.invalid",
              "name": "Invalid",
              "patch": "16.15"
            }
            """));

        Assert.Throws<JsonException>(() => MapControlPackLoader.Load(stream));
    }
}
