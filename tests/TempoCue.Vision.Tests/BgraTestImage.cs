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

    public void DrawBorder(
        int x,
        int y,
        int width,
        int height,
        byte blue,
        byte green,
        byte red)
    {
        for (var offsetX = 0; offsetX < width; offsetX++)
        {
            SetPixel(x + offsetX, y, blue, green, red);
            SetPixel(x + offsetX, y + height - 1, blue, green, red);
        }

        for (var offsetY = 1; offsetY < height - 1; offsetY++)
        {
            SetPixel(x, y + offsetY, blue, green, red);
            SetPixel(x + width - 1, y + offsetY, blue, green, red);
        }
    }

    public void DrawFilledRectangle(
        int x,
        int y,
        int width,
        int height,
        byte blue,
        byte green,
        byte red)
    {
        for (var offsetY = 0; offsetY < height; offsetY++)
        {
            for (var offsetX = 0; offsetX < width; offsetX++)
            {
                SetPixel(x + offsetX, y + offsetY, blue, green, red);
            }
        }
    }

    public void DrawCheckerboard(
        int x,
        int y,
        int width,
        int height,
        (byte Blue, byte Green, byte Red) first,
        (byte Blue, byte Green, byte Red) second)
    {
        for (var offsetY = 0; offsetY < height; offsetY++)
        {
            for (var offsetX = 0; offsetX < width; offsetX++)
            {
                var color = (offsetX + offsetY) % 2 == 0 ? first : second;
                SetPixel(
                    x + offsetX,
                    y + offsetY,
                    color.Blue,
                    color.Green,
                    color.Red);
            }
        }
    }

    public void DrawCircularRing(
        int centerX,
        int centerY,
        int innerRadius,
        int outerRadius,
        byte blue,
        byte green,
        byte red)
    {
        var innerSquared = checked(innerRadius * innerRadius);
        var outerSquared = checked(outerRadius * outerRadius);
        for (var offsetY = -outerRadius; offsetY <= outerRadius; offsetY++)
        {
            for (var offsetX = -outerRadius; offsetX <= outerRadius; offsetX++)
            {
                var distanceSquared = checked(
                    (offsetX * offsetX) + (offsetY * offsetY));
                if (distanceSquared < innerSquared || distanceSquared > outerSquared)
                {
                    continue;
                }

                SetPixel(
                    centerX + offsetX,
                    centerY + offsetY,
                    blue,
                    green,
                    red);
            }
        }
    }

    public void DrawTexturedCircularMarker(
        int centerX,
        int centerY,
        int innerRadius,
        int outerRadius,
        (byte Blue, byte Green, byte Red) ring,
        (byte Blue, byte Green, byte Red) firstInterior,
        (byte Blue, byte Green, byte Red) secondInterior)
    {
        DrawCircularRing(
            centerX,
            centerY,
            innerRadius,
            outerRadius,
            ring.Blue,
            ring.Green,
            ring.Red);

        var halfInterior = Math.Max(1, (int)Math.Floor(innerRadius / Math.Sqrt(2)));
        DrawCheckerboard(
            centerX - halfInterior,
            centerY - halfInterior,
            (halfInterior * 2) + 1,
            (halfInterior * 2) + 1,
            firstInterior,
            secondInterior);
    }
}
