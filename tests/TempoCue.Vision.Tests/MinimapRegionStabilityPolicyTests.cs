namespace TempoCue.Vision.Tests;

/// <summary>
/// Проверяет правило смены области миникарты. Дефект TC-012: одиночная неверная
/// находка заменяла установленную область и на две секунды становилась рабочей.
/// </summary>
public sealed class MinimapRegionStabilityPolicyTests
{
    private static readonly PixelRegion Minimap = new(3071, 1071, 369, 369);

    /// <summary>Кусок карты из TC-012: около трети стороны настоящей области.</summary>
    private static readonly PixelRegion Fragment = new(3071, 1071, 123, 123);

    [Fact]
    public void Decide_AcceptsTheFirstFindWhenNothingIsSettled()
    {
        // Иначе распознавание не начнётся никогда.
        Assert.Equal(
            MinimapRegionStabilityPolicy.Decision.Accept,
            MinimapRegionStabilityPolicy.Decide(null, null, Minimap));
    }

    [Fact]
    public void Decide_HoldsASingleDisagreeingFind()
    {
        // Ровно случай TC-012: кусок карты не имеет права стать рабочей областью
        // с первого раза.
        Assert.Equal(
            MinimapRegionStabilityPolicy.Decision.Hold,
            MinimapRegionStabilityPolicy.Decide(Minimap, null, Fragment));
    }

    [Fact]
    public void Decide_AcceptsWhenTheSameNewSizeRepeats()
    {
        // Игрок и правда сменил размер миникарты: две находки подряд согласны.
        Assert.Equal(
            MinimapRegionStabilityPolicy.Decision.Accept,
            MinimapRegionStabilityPolicy.Decide(Minimap, Fragment, Fragment));
    }

    [Fact]
    public void Decide_RejectsWhenTwoDisagreeingFindsDisagreeWithEachOther()
    {
        // Разброд в находках — не смена размера. Держим установленную область.
        var other = new PixelRegion(3071, 1071, 200, 200);

        Assert.Equal(
            MinimapRegionStabilityPolicy.Decision.Reject,
            MinimapRegionStabilityPolicy.Decide(Minimap, Fragment, other));
    }

    [Theory]
    [InlineData(369)]
    [InlineData(365)]
    [InlineData(375)]
    [InlineData(410)]
    public void Decide_TreatsSmallBoundaryJitterAsTheSameRegion(int width)
    {
        // Граница дрожит на единицы пикселей от кадра к кадру, и это безвредно:
        // задача правила — ловить подмену области куском, а не дрожание.
        var jittered = new PixelRegion(3071, 1071, width, width);

        Assert.Equal(
            MinimapRegionStabilityPolicy.Decision.Accept,
            MinimapRegionStabilityPolicy.Decide(Minimap, null, jittered));
    }
}
