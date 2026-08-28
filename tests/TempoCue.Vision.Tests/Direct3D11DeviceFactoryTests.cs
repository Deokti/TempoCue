namespace TempoCue.Vision.Tests;

public sealed class Direct3D11DeviceFactoryTests
{
    [Fact]
    public void Create_ReturnsWindowsRuntimeDirect3DDevice()
    {
        var device = Direct3D11DeviceFactory.Create();

        Assert.NotNull(device);
        (device as IDisposable)?.Dispose();
    }
}
