using System.IO.Compression;
using System.Text.Json;

namespace TempoCue.Vision;

/// <summary>
/// Читает и записывает один диагностический кадр в переносимый контейнер без Riot ID.
/// </summary>
public static class MinimapReplaySampleStore
{
    public const int CurrentSchemaVersion = 1;
    private const string ManifestEntryName = "manifest.json";
    private const string FrameEntryName = "frame.bgra";
    private const int MaximumImageSide = 1024;
    private const int MaximumManifestLength = 1024 * 1024;
    private const int MaximumCandidateCount = 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task SaveAsync(
        Stream destination,
        MinimapReplaySample sample,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sample);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("Поток должен поддерживать запись.", nameof(destination));
        }

        using var archive = new ZipArchive(
            destination,
            ZipArchiveMode.Create,
            leaveOpen: true);
        await WriteEntryAsync(
            archive,
            FrameEntryName,
            sample.BgraPixels,
            cancellationToken).ConfigureAwait(false);

        var referenceManifests = new List<ReferenceManifest>(
            sample.ChampionReferences.Count);
        for (var index = 0; index < sample.ChampionReferences.Count; index++)
        {
            var reference = sample.ChampionReferences[index];
            var entryName = $"references/{index:D2}.bgra";
            await WriteEntryAsync(
                archive,
                entryName,
                reference.BgraPixels,
                cancellationToken).ConfigureAwait(false);
            referenceManifests.Add(new ReferenceManifest(
                reference.ChampionId,
                reference.Affiliation,
                entryName));
        }

        var manifest = new ReplayManifest(
            CurrentSchemaVersion,
            sample.CapturedAt,
            sample.ImageWidth,
            sample.ImageHeight,
            sample.ImageStride,
            FrameEntryName,
            referenceManifests.ToArray(),
            sample.Baseline);
        var manifestEntry = archive.CreateEntry(
            ManifestEntryName,
            CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(
            manifestStream,
            manifest,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<MinimapReplaySample> LoadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("Поток должен поддерживать чтение.", nameof(source));
        }

        using var archive = new ZipArchive(
            source,
            ZipArchiveMode.Read,
            leaveOpen: true);
        var manifestEntry = GetRequiredEntry(archive, ManifestEntryName);
        if (manifestEntry.Length is <= 0 or > MaximumManifestLength)
        {
            throw new InvalidDataException("Размер манифеста диагностического кадра некорректен.");
        }

        await using var manifestStream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<ReplayManifest>(
            manifestStream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false) ??
            throw new InvalidDataException("Манифест диагностического кадра пуст.");
        ValidateManifest(manifest);

        var imageLength = checked(manifest.ImageStride * manifest.ImageHeight);
        var pixels = await ReadEntryAsync(
            archive,
            manifest.FrameEntryName,
            imageLength,
            cancellationToken).ConfigureAwait(false);
        var references = new MinimapChampionReference[manifest.References.Length];
        var referenceLength = checked(
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.BytesPerPixel);
        for (var index = 0; index < manifest.References.Length; index++)
        {
            var reference = manifest.References[index];
            var referencePixels = await ReadEntryAsync(
                archive,
                reference.EntryName,
                referenceLength,
                cancellationToken).ConfigureAwait(false);
            references[index] = new MinimapChampionReference(
                reference.ChampionId,
                reference.Affiliation,
                referencePixels);
        }

        return new MinimapReplaySample(
            manifest.CapturedAt,
            manifest.ImageWidth,
            manifest.ImageHeight,
            manifest.ImageStride,
            pixels,
            references,
            manifest.Baseline);
    }

    private static async Task WriteEntryAsync(
        ZipArchive archive,
        string entryName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadEntryAsync(
        ZipArchive archive,
        string entryName,
        int expectedLength,
        CancellationToken cancellationToken)
    {
        var entry = GetRequiredEntry(archive, entryName);
        if (entry.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"Размер элемента '{entryName}' не соответствует манифесту.");
        }

        var result = GC.AllocateUninitializedArray<byte>(expectedLength);
        await using var stream = entry.Open();
        await stream.ReadExactlyAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static ZipArchiveEntry GetRequiredEntry(ZipArchive archive, string entryName) =>
        archive.GetEntry(entryName) ?? throw new InvalidDataException(
            $"В диагностическом контейнере отсутствует '{entryName}'.");

    private static void ValidateManifest(ReplayManifest manifest)
    {
        if (manifest.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Версия диагностического кадра {manifest.SchemaVersion} не поддерживается.");
        }

        if (manifest.ImageWidth is <= 0 or > MaximumImageSide ||
            manifest.ImageHeight is <= 0 or > MaximumImageSide ||
            manifest.ImageStride != checked(manifest.ImageWidth * 4))
        {
            throw new InvalidDataException("Геометрия диагностического кадра некорректна.");
        }

        if (!string.Equals(manifest.FrameEntryName, FrameEntryName, StringComparison.Ordinal) ||
            manifest.References is null ||
            manifest.References.Length > 10 ||
            manifest.Baseline is null ||
            manifest.Baseline.MarkerCandidates is null ||
            manifest.Baseline.ChampionCandidates is null ||
            manifest.Baseline.MarkerCandidates.Length > MaximumCandidateCount ||
            manifest.Baseline.ChampionCandidates.Length > MaximumCandidateCount ||
            manifest.Baseline.ChampionProposalCount < 0 ||
            !double.IsFinite(manifest.Baseline.MaximumIdentityConfidence))
        {
            throw new InvalidDataException("Манифест диагностического кадра некорректен.");
        }

        if (manifest.References.Any(static reference =>
                string.IsNullOrWhiteSpace(reference.ChampionId) ||
                !Enum.IsDefined(reference.Affiliation) ||
                string.IsNullOrWhiteSpace(reference.EntryName)))
        {
            throw new InvalidDataException("Описание эталонов участников некорректно.");
        }
    }

    private sealed record ReplayManifest(
        int SchemaVersion,
        DateTimeOffset CapturedAt,
        int ImageWidth,
        int ImageHeight,
        int ImageStride,
        string FrameEntryName,
        ReferenceManifest[] References,
        MinimapReplayOutcome Baseline);

    private sealed record ReferenceManifest(
        string ChampionId,
        MinimapMarkerAffiliation Affiliation,
        string EntryName);
}
