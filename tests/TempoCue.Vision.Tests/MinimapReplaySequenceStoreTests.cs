using TempoCue.Vision;

namespace TempoCue.Vision.Tests;

/// <summary>
/// Запись последовательности делается один раз в матче и читается потом. Ошибка
/// формата обнаружится, когда матч уже не повторить, поэтому чтение проверяется
/// сразу после записи.
/// </summary>
public sealed class MinimapReplaySequenceStoreTests
{
    private const int Side = MinimapFrameNormalizer.CanonicalSize;
    private const int Stride = Side * 4;

    [Fact]
    public async Task SavedSequence_RoundTripsFramesAndReferences()
    {
        var sequence = CreateSequence(frameCount: 3);
        using var buffer = new MemoryStream();

        await MinimapReplaySequenceStore.SaveAsync(buffer, sequence);
        buffer.Position = 0;
        var loaded = await MinimapReplaySequenceStore.LoadAsync(buffer);

        Assert.Equal(sequence.Frames.Count, loaded.Frames.Count);
        Assert.Equal(sequence.ImageWidth, loaded.ImageWidth);
        Assert.Equal(sequence.ImageStride, loaded.ImageStride);
        for (var index = 0; index < sequence.Frames.Count; index++)
        {
            Assert.Equal(sequence.Frames[index].CapturedAt, loaded.Frames[index].CapturedAt);
            // Кадры обязаны отличаться друг от друга и сохранять порядок: запись
            // без этого не измеряет ничего, кроме одного и того же кадра трижды.
            Assert.Equal(
                sequence.Frames[index].BgraPixels.ToArray(),
                loaded.Frames[index].BgraPixels.ToArray());
        }

        Assert.Equal(
            sequence.ChampionReferences.Select(reference => reference.ChampionId),
            loaded.ChampionReferences.Select(reference => reference.ChampionId));
    }

    [Fact]
    public async Task LoadedSequence_ExposesEachFrameAsOrdinarySample()
    {
        var sequence = CreateSequence(frameCount: 2);
        using var buffer = new MemoryStream();
        await MinimapReplaySequenceStore.SaveAsync(buffer, sequence);
        buffer.Position = 0;
        var loaded = await MinimapReplaySequenceStore.LoadAsync(buffer);

        // Существующие инструменты работают с одиночными кадрами, и запись обязана
        // разбираться на них без преобразований.
        var sample = loaded.ToSample(1);

        Assert.Equal(Side, sample.ImageWidth);
        Assert.Equal(loaded.Frames[1].CapturedAt, sample.CapturedAt);
        Assert.Equal(loaded.ChampionReferences.Count, sample.ChampionReferences.Count);
    }

    [Fact]
    public void Duration_MeasuresSpanBetweenFirstAndLastFrame()
    {
        var sequence = CreateSequence(frameCount: 5);

        Assert.Equal(TimeSpan.FromMilliseconds(125 * 4), sequence.Duration);
    }

    [Fact]
    public void EmptySequence_IsRejected() =>
        Assert.Throws<ArgumentException>(() => new MinimapReplaySequence(
            Side,
            Side,
            Stride,
            [],
            []));

    private static MinimapReplaySequence CreateSequence(int frameCount)
    {
        var start = new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
        var frames = new List<MinimapReplaySequenceFrame>(frameCount);
        for (var index = 0; index < frameCount; index++)
        {
            var pixels = new byte[Stride * Side];
            // Каждому кадру своя заливка, чтобы перепутанный порядок был виден.
            Array.Fill(pixels, (byte)(index + 1));
            frames.Add(new MinimapReplaySequenceFrame(
                start.AddMilliseconds(125 * index),
                pixels));
        }

        var referencePixels = new byte[
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.BytesPerPixel];
        return new MinimapReplaySequence(
            Side,
            Side,
            Stride,
            frames,
            [
                new MinimapChampionReference("Nasus", MinimapMarkerAffiliation.Ally, referencePixels),
                new MinimapChampionReference("Kayle", MinimapMarkerAffiliation.Enemy, referencePixels)
            ]);
    }
}
