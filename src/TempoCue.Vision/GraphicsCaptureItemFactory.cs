using System.Runtime.InteropServices;
using Windows.Graphics.Capture;

namespace TempoCue.Vision;

/// <summary>Создаёт Windows Graphics Capture item для заранее проверенного HWND.</summary>
internal static class GraphicsCaptureItemFactory
{
    private const string RuntimeClassName = "Windows.Graphics.Capture.GraphicsCaptureItem";
    private static readonly Guid GraphicsCaptureItemInteropId =
        new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid GraphicsCaptureItemId =
        new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    public static GraphicsCaptureItem CreateForWindow(nint windowHandle)
    {
        ArgumentOutOfRangeException.ThrowIfZero(windowHandle);

        nint className = nint.Zero;
        nint factoryPointer = nint.Zero;
        nint itemPointer = nint.Zero;
        object? factoryObject = null;
        try
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(
                RuntimeClassName,
                RuntimeClassName.Length,
                out className));
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(
                className,
                GraphicsCaptureItemInteropId,
                out factoryPointer));
            factoryObject = Marshal.GetObjectForIUnknown(factoryPointer);
            var interop = (IGraphicsCaptureItemInterop)factoryObject;
            Marshal.ThrowExceptionForHR(interop.CreateForWindow(
                windowHandle,
                GraphicsCaptureItemId,
                out itemPointer));
            return WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
        }
        finally
        {
            if (itemPointer != nint.Zero)
            {
                WinRT.MarshalInterface<GraphicsCaptureItem>.DisposeAbi(itemPointer);
            }

            if (factoryObject is not null && Marshal.IsComObject(factoryObject))
            {
                Marshal.ReleaseComObject(factoryObject);
            }

            if (factoryPointer != nint.Zero)
            {
                Marshal.Release(factoryPointer);
            }

            if (className != nint.Zero)
            {
                WindowsDeleteString(className);
            }
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig]
        int CreateForWindow(nint window, in Guid interfaceId, out nint result);

        [PreserveSig]
        int CreateForMonitor(nint monitor, in Guid interfaceId, out nint result);
    }

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string source,
        int length,
        out nint value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(
        nint activatableClassId,
        in Guid interfaceId,
        out nint factory);
}
