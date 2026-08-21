using TempoCue.Vision;

namespace TempoCue.Vision.Tests;

public sealed class MinimapNegativeReferenceTests
{
    [Fact]
    public void Catalog_LoadsEmbeddedTemplatesOfCanonicalSize()
    {
        var templates = MinimapNegativeReferenceCatalog.LoadBgraTemplates();

        var expectedLength =
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.BytesPerPixel;
        Assert.Equal(9, templates.Count);
        Assert.All(templates, template => Assert.Equal(expectedLength, template.Length));
    }

    [Fact]
    public void Recognize_RejectsRegionMatchingNegativeReferenceAtLeastAsWellAsPortrait()
    {
        // Кадр содержит пиксели встроенного эталона башни, а эталон «чемпиона»
        // составлен из тех же пикселей: сходство с башней не слабее сходства с
        // портретом, поэтому подтверждение обязано быть отклонено.
        var towerTemplate = MinimapNegativeReferenceCatalog.LoadBgraTemplates()[^1];
        const int side = MinimapChampionReference.ImageSide;
        const int imageSize = 64;
        var pixels = new byte[imageSize * imageSize * 4];
        for (var index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }

        const int pasteX = 20;
        const int pasteY = 20;
        for (var row = 0; row < side; row++)
        {
            Array.Copy(
                towerTemplate,
                row * side * 4,
                pixels,
                (((pasteY + row) * imageSize) + pasteX) * 4,
                side * 4);
        }

        var reference = new MinimapChampionReference(
            "FakeChampion",
            MinimapMarkerAffiliation.Ally,
            towerTemplate);
        MinimapChampionMarkerCandidate[] candidates =
        [
            new(
                pasteX + (side / 2) - 12,
                pasteY + (side / 2) - 12,
                25,
                25,
                MinimapMarkerAffiliation.Ally,
                0.72)
        ];

        using var recognizer = new OpenCvMinimapChampionRecognizer();
        var result = recognizer.Recognize(
            pixels,
            imageSize,
            imageSize,
            imageSize * 4,
            candidates,
            [reference]);

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Recognize_KeepsPortraitThatDoesNotResembleNegativeReferences()
    {
        // Контрольный сценарий: обычный контрастный портрет не похож на статичные
        // объекты и обязан подтверждаться так же, как до появления негативов.
        var image = new BgraTestImage(64, 64, 20, 30, 25);
        image.DrawCheckerboard(
            x: 20,
            y: 20,
            width: MinimapChampionReference.ImageSide,
            height: MinimapChampionReference.ImageSide,
            first: (25, 65, 185),
            second: (180, 215, 225));
        var reference = MinimapRecognitionTestData.CreateReference(
            image,
            20,
            20,
            "ExampleChampion");

        using var recognizer = new OpenCvMinimapChampionRecognizer();
        var result = recognizer.Recognize(
            image.Pixels,
            image.Width,
            image.Height,
            image.Stride,
            [new(16, 16, 25, 25, MinimapMarkerAffiliation.Ally, 0.72)],
            [reference]);

        var match = Assert.Single(result.Candidates);
        Assert.Equal("ExampleChampion", match.ChampionId);
    }
}
