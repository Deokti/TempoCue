using System.IO.Compression;
using System.Text.Json;

namespace TempoCue.Vision;

/// <summary>
/// Читает и записывает последовательность кадров в тот же переносимый контейнер,
/// что и одиночный кадр: ZIP без Riot ID и имён призывателей. Эталоны состава
/// хранятся один раз на всю запись — внутри матча ростер не меняется, и повторять
/// их на каждом кадре значит раздувать файл впустую.
/// </summary>
public static class MinimapReplaySequenceStore
{
    public const int CurrentSchemaVersion = 1;
    public const string SidecarSuffix = ".labels.json";

    /// <summary>
    /// Верхний предел записи. Три секунды при восьми кадрах в секунду дают около
    /// двадцати пяти кадров; сотня оставляет запас и не даёт файлу вырасти
    /// непредсказуемо.
    /// </summary>
    public const int MaximumFrameCount = 100;

    private const string ManifestEntryName = "manifest.json";
    private const int MaximumImageSide = 1024;
    private const int MaximumManifestLength = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task SaveAsync(
        Stream destination,
        MinimapReplaySequence sequence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sequence);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("Поток должен поддерживать запись.", nameof(destination));
        }

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var frameManifests = new List<SequenceFrameManifest>(sequence.Frames.Count);
        for (var index = 0; index < sequence.Frames.Count; index++)
        {
            var entryName = $"frames/{index:D3}.bgra";
            await WriteEntryAsync(
                archive,
                entryName,
                sequence.Frames[index].BgraPixels,
                cancellationToken).ConfigureAwait(false);
            frameManifests.Add(new SequenceFrameManifest(
                sequence.Frames[index].CapturedAt,
                entryName));
        }

        var referenceManifests = new List<SequenceReferenceManifest>(
            sequence.ChampionReferences.Count);
        for (var index = 0; index < sequence.ChampionReferences.Count; index++)
        {
            var reference = sequence.ChampionReferences[index];
            var entryName = $"references/{index:D2}.bgra";
            await WriteEntryAsync(
                archive,
                entryName,
                reference.BgraPixels,
                cancellationToken).ConfigureAwait(false);
            referenceManifests.Add(new SequenceReferenceManifest(
                reference.ChampionId,
                reference.Affiliation,
                entryName));
        }

        var manifest = new SequenceManifest(
            CurrentSchemaVersion,
            sequence.ImageWidth,
            sequence.ImageHeight,
            sequence.ImageStride,
            [.. frameManifests],
            [.. referenceManifests]);
        var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(
            manifestStream,
            manifest,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<MinimapReplaySequence> LoadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("Поток должен поддерживать чтение.", nameof(source));
        }

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry(ManifestEntryName) ??
            throw new InvalidDataException("В контейнере нет манифеста последовательности.");
        if (manifestEntry.Length is <= 0 or > MaximumManifestLength)
        {
            throw new InvalidDataException("Размер манифеста последовательности некорректен.");
        }

        await using var manifestStream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<SequenceManifest>(
            manifestStream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false) ??
            throw new InvalidDataException("Манифест последовательности пуст.");
        Validate(manifest);

        var imageLength = checked(manifest.ImageStride * manifest.ImageHeight);
        var frames = new MinimapReplaySequenceFrame[manifest.Frames.Length];
        for (var index = 0; index < manifest.Frames.Length; index++)
        {
            frames[index] = new MinimapReplaySequenceFrame(
                manifest.Frames[index].CapturedAt,
                await ReadEntryAsync(
                    archive,
                    manifest.Frames[index].EntryName,
                    imageLength,
                    cancellationToken).ConfigureAwait(false));
        }

        var referenceLength = checked(
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.BytesPerPixel);
        var references = new MinimapChampionReference[manifest.References.Length];
        for (var index = 0; index < manifest.References.Length; index++)
        {
            var reference = manifest.References[index];
            references[index] = new MinimapChampionReference(
                reference.ChampionId,
                reference.Affiliation,
                await ReadEntryAsync(
                    archive,
                    reference.EntryName,
                    referenceLength,
                    cancellationToken).ConfigureAwait(false));
        }

        return new MinimapReplaySequence(
            manifest.ImageWidth,
            manifest.ImageHeight,
            manifest.ImageStride,
            frames,
            references);
    }

    public static async Task<MinimapSequenceGroundTruth?> TryLoadGroundTruthAsync(
        string sequencePath,
        CancellationToken cancellationToken = default)
    {
        var labelsPath = sequencePath + SidecarSuffix;
        if (!File.Exists(labelsPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(labelsPath);
        var truth = await JsonSerializer.DeserializeAsync<MinimapSequenceGroundTruth>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        if (truth is null || truth.SchemaVersion != MinimapSequenceGroundTruth.CurrentSchemaVersion)
        {
            throw new InvalidDataException("Разметка последовательности повреждена.");
        }

        return truth;
    }

    private static void Validate(SequenceManifest manifest)
    {
        if (manifest.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Версия формата {manifest.SchemaVersion} не поддерживается.");
        }

        if (manifest.ImageWidth is <= 0 or > MaximumImageSide ||
            manifest.ImageHeight is <= 0 or > MaximumImageSide)
        {
            throw new InvalidDataException("Геометрия кадров последовательности некорректна.");
        }

        if (manifest.Frames.Length is <= 0 or > MaximumFrameCount)
        {
            throw new InvalidDataException("Число кадров последовательности некорректно.");
        }
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
        var entry = archive.GetEntry(entryName) ??
            throw new InvalidDataException($"В контейнере нет записи {entryName}.");
        if (entry.Length != expectedLength)
        {
            throw new InvalidDataException($"Размер записи {entryName} не соответствует манифесту.");
        }

        var buffer = new byte[expectedLength];
        await using var stream = entry.Open();
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer;
    }

    private sealed record SequenceManifest(
        int SchemaVersion,
        int ImageWidth,
        int ImageHeight,
        int ImageStride,
        SequenceFrameManifest[] Frames,
        SequenceReferenceManifest[] References);

    private sealed record SequenceFrameManifest(DateTimeOffset CapturedAt, string EntryName);

    private sealed record SequenceReferenceManifest(
        string ChampionId,
        MinimapMarkerAffiliation Affiliation,
        string EntryName);
}
