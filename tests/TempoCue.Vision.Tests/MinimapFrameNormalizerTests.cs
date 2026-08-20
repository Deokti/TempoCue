namespace TempoCue.Vision.Tests;

public sealed class MinimapFrameNormalizerTests
{
    [Fact]
    public void Normalize_CopiesIdentityImageWithoutIncludingStridePadding()
    {
        byte[] source =
        [
            1, 2, 3, 4, 5, 6, 7, 8, 90, 91, 92, 93,
            9, 10, 11, 12, 13, 14, 15, 16, 94, 95, 96, 97
        ];
        var destination = new byte[16];

        MinimapFrameNormalizer.Normalize(
            source,
            sourceWidth: 2,
            sourceHeight: 2,
            sourceStride: 12,
            destination,
            destinationWidth: 2,
            destinationHeight: 2,
            destinationStride: 8);

        Assert.Equal(
            new byte[]
            {
                1, 2, 3, 4, 5, 6, 7, 8,
                9, 10, 11, 12, 13, 14, 15, 16
            },
            destination);
    }

    [Fact]
    public void Normalize_UsesBilinearInterpolationAndPreservesCorners()
    {
        byte[] source =
        [
            0, 0, 0, 255, 100, 100, 100, 255,
            200, 200, 200, 255, 255, 255, 255, 255
        ];
        var destination = new byte[3 * 3 * 4];

        MinimapFrameNormalizer.Normalize(
            source,
            sourceWidth: 2,
            sourceHeight: 2,
            sourceStride: 8,
            destination,
            destinationWidth: 3,
            destinationHeight: 3,
            destinationStride: 12);

        AssertPixel(destination, stride: 12, x: 0, y: 0, expected: 0);
        AssertPixel(destination, stride: 12, x: 2, y: 0, expected: 100);
        AssertPixel(destination, stride: 12, x: 0, y: 2, expected: 200);
        AssertPixel(destination, stride: 12, x: 2, y: 2, expected: 255);
        AssertPixel(destination, stride: 12, x: 1, y: 1, expected: 139);
    }

    [Fact]
    public void Normalize_RejectsIncompleteSourceBuffer()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            MinimapFrameNormalizer.Normalize(
                new byte[15],
                sourceWidth: 2,
                sourceHeight: 2,
                sourceStride: 8));

        Assert.Equal("sourcePixels", exception.ParamName);
    }

    private static void AssertPixel(
        byte[] pixels,
        int stride,
        int x,
        int y,
        byte expected)
    {
        var offset = (y * stride) + (x * 4);
        Assert.Equal(expected, pixels[offset]);
        Assert.Equal(expected, pixels[offset + 1]);
        Assert.Equal(expected, pixels[offset + 2]);
        Assert.Equal(255, pixels[offset + 3]);
    }
}
