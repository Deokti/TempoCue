using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;

namespace TempoCue.Vision;

/// <summary>Копирует с GPU в CPU только выбранную область поверхности Direct3D 11.</summary>
internal sealed class Direct3D11MinimapPreviewReader : IDisposable
{
    private const int BytesPerPixel = 4;
    private static readonly TimeSpan DetectionRefreshInterval = TimeSpan.FromSeconds(2);
    private const uint D3d11CpuAccessRead = 0x20000;
    private const uint D3d11MapRead = 1;
    private const uint D3d11UsageStaging = 3;
    private const uint DxgiFormatB8G8R8A8UNorm = 87;
    private const int DeviceCreateTexture2DSlot = 5;
    private const int DeviceGetImmediateContextSlot = 40;
    private const int DeviceChildGetDeviceSlot = 3;
    private const int ContextMapSlot = 14;
    private const int ContextUnmapSlot = 15;
    private const int ContextCopySubresourceRegionSlot = 46;
    private const int InterfaceAccessGetInterfaceSlot = 3;

    private static readonly Guid Direct3DDxgiInterfaceAccessGuid =
        new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");
    private static readonly Guid Direct3D11Texture2DGuid =
        new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    private nint _device;
    private nint _context;
    private nint _stagingTexture;
    private int _stagingWidth;
    private int _stagingHeight;
    private PixelRegion? _detectedRegion;
    private MinimapScreenCorner _detectedCorner;
    private double _detectionConfidence;

    /// <summary>Непринятая находка прошлого пересчёта: ждёт подтверждения.</summary>
    private PixelRegion? _candidateRegion;
    private DateTimeOffset? _lastDetectionAt;
    private int _frameWidth;
    private int _frameHeight;
    private byte[]? _leftSearchBuffer;
    private byte[]? _rightSearchBuffer;
    private byte[]? _minimapBuffer;
    private bool _isDisposed;

    public MinimapPreviewSnapshot Read(
        IDirect3DSurface surface,
        int frameWidth,
        int frameHeight,
        DateTimeOffset capturedAt)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(surface);

        var sourceTexture = GetTexture(surface);
        try
        {
            EnsureDevice(sourceTexture);
            var rightSearchRegion = MinimapRegionCalculator.CalculateSearchRegion(
                frameWidth,
                frameHeight,
                MinimapScreenCorner.BottomRight);
            EnsureStagingTexture(rightSearchRegion.Width, rightSearchRegion.Height);
            ResetDetectionWhenFrameChanges(frameWidth, frameHeight);

            if (ShouldRefreshDetection(capturedAt))
            {
                TryRefreshDetection(
                    sourceTexture,
                    frameWidth,
                    frameHeight,
                    capturedAt,
                    rightSearchRegion);
            }

            var region = _detectedRegion ?? throw new InvalidOperationException(
                "Граница миникарты не найдена в нижних углах кадра.");
            var pixels = ReadRegion(sourceTexture, region, ref _minimapBuffer);
            var normalizedPixels = MinimapFrameNormalizer.Normalize(
                pixels,
                region.Width,
                region.Height,
                checked(region.Width * BytesPerPixel));
            return new MinimapPreviewSnapshot(
                region.X,
                region.Y,
                region.Width,
                region.Height,
                MinimapFrameNormalizer.CanonicalSize,
                MinimapFrameNormalizer.CanonicalSize,
                checked(MinimapFrameNormalizer.CanonicalSize * BytesPerPixel),
                normalizedPixels,
                capturedAt,
                _detectedCorner,
                _detectionConfidence);
        }
        finally
        {
            Marshal.Release(sourceTexture);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _detectedRegion = null;
        _lastDetectionAt = null;
        ClearBuffer(ref _leftSearchBuffer);
        ClearBuffer(ref _rightSearchBuffer);
        ClearBuffer(ref _minimapBuffer);
        Release(ref _stagingTexture);
        Release(ref _context);
        Release(ref _device);
    }

    private bool ShouldRefreshDetection(DateTimeOffset capturedAt) =>
        _detectedRegion is null ||
        _lastDetectionAt is null ||
        capturedAt - _lastDetectionAt >= DetectionRefreshInterval;

    private void TryRefreshDetection(
        nint sourceTexture,
        int frameWidth,
        int frameHeight,
        DateTimeOffset capturedAt,
        PixelRegion rightSearchRegion)
    {
        var leftSearchRegion = MinimapRegionCalculator.CalculateSearchRegion(
            frameWidth,
            frameHeight,
            MinimapScreenCorner.BottomLeft);
        var leftPixels = ReadRegion(
            sourceTexture,
            leftSearchRegion,
            ref _leftSearchBuffer);
        var rightPixels = ReadRegion(
            sourceTexture,
            rightSearchRegion,
            ref _rightSearchBuffer);
        var detection = MinimapRegionDetector.Detect(
            leftPixels,
            rightPixels,
            rightSearchRegion.Width,
            checked(rightSearchRegion.Width * BytesPerPixel),
            frameWidth,
            frameHeight);
        _lastDetectionAt = capturedAt;

        if (detection is null)
        {
            return;
        }

        // Одиночная неверная находка не имеет права сменить установленную
        // область: TC-012, кусок карты прошёл нижнюю границу размера и на две
        // секунды стал рабочей областью.
        switch (MinimapRegionStabilityPolicy.Decide(
            _detectedRegion,
            _candidateRegion,
            detection.Region))
        {
            case MinimapRegionStabilityPolicy.Decision.Hold:
                _candidateRegion = detection.Region;
                return;
            case MinimapRegionStabilityPolicy.Decision.Reject:
                _candidateRegion = null;
                return;
        }

        _candidateRegion = null;
        _detectedRegion = detection.Region;
        _detectedCorner = detection.Corner;
        _detectionConfidence = detection.Confidence;
    }

    private void ResetDetectionWhenFrameChanges(int frameWidth, int frameHeight)
    {
        if (_frameWidth == frameWidth && _frameHeight == frameHeight)
        {
            return;
        }

        _frameWidth = frameWidth;
        _frameHeight = frameHeight;
        _detectedRegion = null;
        _candidateRegion = null;
        _lastDetectionAt = null;
    }

    private ReadOnlySpan<byte> ReadRegion(
        nint sourceTexture,
        PixelRegion region,
        ref byte[]? buffer)
    {
        CopyRegion(sourceTexture, region);
        return ReadStagingTexture(region.Width, region.Height, ref buffer);
    }

    private static nint GetTexture(IDirect3DSurface surface)
    {
        var surfaceAbi = WinRT.MarshalInterface<IDirect3DSurface>.FromManaged(surface);
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(
                surfaceAbi,
                in Direct3DDxgiInterfaceAccessGuid,
                out var interfaceAccess));
            try
            {
                var getInterface = GetDelegate<GetInterfaceDelegate>(
                    interfaceAccess,
                    InterfaceAccessGetInterfaceSlot);
                Marshal.ThrowExceptionForHR(getInterface(
                    interfaceAccess,
                    in Direct3D11Texture2DGuid,
                    out var texture));
                return texture;
            }
            finally
            {
                Marshal.Release(interfaceAccess);
            }
        }
        finally
        {
            WinRT.MarshalInterface<IDirect3DSurface>.DisposeAbi(surfaceAbi);
        }
    }

    private void EnsureDevice(nint sourceTexture)
    {
        if (_device != 0 && _context != 0)
        {
            return;
        }

        var getDevice = GetDelegate<GetDeviceDelegate>(
            sourceTexture,
            DeviceChildGetDeviceSlot);
        getDevice(sourceTexture, out _device);
        if (_device == 0)
        {
            throw new InvalidOperationException("Direct3D не вернул устройство поверхности кадра.");
        }

        var getImmediateContext = GetDelegate<GetImmediateContextDelegate>(
            _device,
            DeviceGetImmediateContextSlot);
        getImmediateContext(_device, out _context);
        if (_context == 0)
        {
            Release(ref _device);
            throw new InvalidOperationException("Direct3D не вернул контекст устройства.");
        }
    }

    private void EnsureStagingTexture(int width, int height)
    {
        if (_stagingTexture != 0 && _stagingWidth == width && _stagingHeight == height)
        {
            return;
        }

        Release(ref _stagingTexture);
        var description = new Texture2DDescription
        {
            Width = checked((uint)width),
            Height = checked((uint)height),
            MipLevels = 1,
            ArraySize = 1,
            Format = DxgiFormatB8G8R8A8UNorm,
            SampleDescription = new SampleDescription { Count = 1 },
            Usage = D3d11UsageStaging,
            CpuAccessFlags = D3d11CpuAccessRead
        };
        var createTexture = GetDelegate<CreateTexture2DDelegate>(
            _device,
            DeviceCreateTexture2DSlot);
        Marshal.ThrowExceptionForHR(createTexture(
            _device,
            in description,
            0,
            out _stagingTexture));
        _stagingWidth = width;
        _stagingHeight = height;
    }

    private void CopyRegion(nint sourceTexture, PixelRegion region)
    {
        var sourceBox = new D3d11Box
        {
            Left = checked((uint)region.X),
            Top = checked((uint)region.Y),
            Front = 0,
            Right = checked((uint)(region.X + region.Width)),
            Bottom = checked((uint)(region.Y + region.Height)),
            Back = 1
        };
        var copyRegion = GetDelegate<CopySubresourceRegionDelegate>(
            _context,
            ContextCopySubresourceRegionSlot);
        copyRegion(
            _context,
            _stagingTexture,
            0,
            0,
            0,
            0,
            sourceTexture,
            0,
            in sourceBox);
    }

    private ReadOnlySpan<byte> ReadStagingTexture(
        int width,
        int height,
        ref byte[]? buffer)
    {
        var map = GetDelegate<MapDelegate>(_context, ContextMapSlot);
        Marshal.ThrowExceptionForHR(map(
            _context,
            _stagingTexture,
            0,
            D3d11MapRead,
            0,
            out var mapped));
        try
        {
            var targetStride = checked(width * BytesPerPixel);
            var requiredLength = checked(targetStride * height);
            var targetBuffer = EnsureBuffer(ref buffer, requiredLength);
            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(
                    nint.Add(mapped.Data, checked((int)(row * mapped.RowPitch))),
                    targetBuffer,
                    checked(row * targetStride),
                    targetStride);
            }

            return targetBuffer.AsSpan(0, requiredLength);
        }
        finally
        {
            var unmap = GetDelegate<UnmapDelegate>(_context, ContextUnmapSlot);
            unmap(_context, _stagingTexture, 0);
        }
    }

    private static byte[] EnsureBuffer(ref byte[]? buffer, int requiredLength)
    {
        if (buffer is not null && buffer.Length >= requiredLength)
        {
            return buffer;
        }

        buffer = GC.AllocateUninitializedArray<byte>(requiredLength);
        return buffer;
    }

    private static void ClearBuffer(ref byte[]? buffer)
    {
        if (buffer is null)
        {
            return;
        }

        Array.Clear(buffer);
        buffer = null;
    }

    private static TDelegate GetDelegate<TDelegate>(nint instance, int slot)
        where TDelegate : Delegate
    {
        var vtable = Marshal.ReadIntPtr(instance);
        var function = Marshal.ReadIntPtr(vtable, checked(slot * IntPtr.Size));
        return Marshal.GetDelegateForFunctionPointer<TDelegate>(function);
    }

    private static void Release(ref nint pointer)
    {
        if (pointer == 0)
        {
            return;
        }

        Marshal.Release(pointer);
        pointer = 0;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetInterfaceDelegate(nint @this, in Guid interfaceId, out nint result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GetDeviceDelegate(nint @this, out nint device);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GetImmediateContextDelegate(nint @this, out nint context);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateTexture2DDelegate(
        nint @this,
        in Texture2DDescription description,
        nint initialData,
        out nint texture);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void CopySubresourceRegionDelegate(
        nint @this,
        nint destination,
        uint destinationSubresource,
        uint destinationX,
        uint destinationY,
        uint destinationZ,
        nint source,
        uint sourceSubresource,
        in D3d11Box sourceBox);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int MapDelegate(
        nint @this,
        nint resource,
        uint subresource,
        uint mapType,
        uint mapFlags,
        out MappedSubresource mapped);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void UnmapDelegate(nint @this, nint resource, uint subresource);

    [StructLayout(LayoutKind.Sequential)]
    private struct SampleDescription
    {
        public uint Count;
        public uint Quality;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Texture2DDescription
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public uint Format;
        public SampleDescription SampleDescription;
        public uint Usage;
        public uint BindFlags;
        public uint CpuAccessFlags;
        public uint MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3d11Box
    {
        public uint Left;
        public uint Top;
        public uint Front;
        public uint Right;
        public uint Bottom;
        public uint Back;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MappedSubresource
    {
        public nint Data;
        public uint RowPitch;
        public uint DepthPitch;
    }
}
