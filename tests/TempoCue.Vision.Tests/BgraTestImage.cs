namespace TempoCue.Vision.Tests;

/// <summary>Создаёт небольшие BGRA32-кадры для алгоритмических тестов Vision.</summary>
internal sealed class BgraTestImage
{
    public BgraTestImage(int width, int height, byte blue, byte green, byte red)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Stride = checked(width * 4);
        Pixels = new byte[checked(Stride * height)];

        Fill(blue, green, red);
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public byte[] Pixels { get; }

    public void Fill(byte blue, byte green, byte red)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                SetPixel(x, y, blue, green, red);
            }
        }
    }

    public void SetPixel(int x, int y, byte blue, byte green, byte red)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        var offset = checked((y * Stride) + (x * 4));
        Pixels[offset] = blue;
        Pixels[offset + 1] = green;
        Pixels[offset + 2] = red;
        Pixels[offset + 3] = 255;
    }
}
