using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;

namespace TempoCue.Vision;

internal static class Direct3D11DeviceFactory
{
    private const uint D3d11SdkVersion = 7;
    private const uint D3d11CreateDeviceBgraSupport = 0x20;
    private static readonly Guid IdxgiDeviceGuid = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

    public static IDirect3DDevice Create()
    {
        var result = CreateNativeDevice(D3dDriverType.Hardware, out var nativeDevice);
        if (result < 0)
        {
            result = CreateNativeDevice(D3dDriverType.Warp, out nativeDevice);
        }

        Marshal.ThrowExceptionForHR(result);
        try
        {
            var iid = IdxgiDeviceGuid;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(nativeDevice, in iid, out var dxgiDevice));
            try
            {
                Marshal.ThrowExceptionForHR(
                    CreateDirect3D11DeviceFromDxgiDevice(dxgiDevice, out var inspectableDevice));
                try
                {
                    return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(inspectableDevice);
                }
                finally
                {
                    Marshal.Release(inspectableDevice);
                }
            }
            finally
            {
                Marshal.Release(dxgiDevice);
            }
        }
        finally
        {
            Marshal.Release(nativeDevice);
        }
    }

    private static int CreateNativeDevice(D3dDriverType driverType, out nint nativeDevice)
    {
        var result = D3D11CreateDevice(
            0,
            driverType,
            0,
            D3d11CreateDeviceBgraSupport,
            0,
            0,
            D3d11SdkVersion,
            out nativeDevice,
            out _,
            out var immediateContext);

        if (immediateContext != 0)
        {
            Marshal.Release(immediateContext);
        }

        return result;
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(
        nint adapter,
        D3dDriverType driverType,
        nint software,
        uint flags,
        nint featureLevels,
        uint featureLevelCount,
        uint sdkVersion,
        out nint device,
        out int featureLevel,
        out nint immediateContext);

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDxgiDevice(
        nint dxgiDevice,
        out nint graphicsDevice);

    private enum D3dDriverType
    {
        Hardware = 1,
        Warp = 5
    }
}
