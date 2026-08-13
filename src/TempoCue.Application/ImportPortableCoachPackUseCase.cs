using System.Text.Json;

namespace TempoCue.Application;

/// <summary>Определяет вид переносимого Coach Pack и передаёт его целевому сценарию импорта.</summary>
public sealed class ImportPortableCoachPackUseCase
{
    private readonly ImportCoachPackUseCase _importMatchup;
    private readonly ImportMapControlPackUseCase _importMapControl;

    public ImportPortableCoachPackUseCase(
        ImportCoachPackUseCase importMatchup,
        ImportMapControlPackUseCase importMapControl)
    {
        _importMatchup = importMatchup ?? throw new ArgumentNullException(nameof(importMatchup));
        _importMapControl = importMapControl
            ?? throw new ArgumentNullException(nameof(importMapControl));
    }

    public async Task<PortableCoachPackImportPreview> InspectAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using var document = await JsonDocument.ParseAsync(
            buffer,
            cancellationToken: cancellationToken);
        var packType = GetPackType(document.RootElement);
        buffer.Position = 0;

        if (packType is null)
        {
            var preview = await _importMatchup.InspectAsync(buffer, cancellationToken);
            return PortableCoachPackImportPreview.FromMatchup(preview);
        }

        if (!string.Equals(
                packType,
                MapControlPack.SupportedPackType,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported Coach Pack type: {packType}.");
        }

        var mapControlPreview = await _importMapControl.InspectAsync(
            buffer,
            cancellationToken);
        return PortableCoachPackImportPreview.FromMapControl(mapControlPreview);
    }

    public async Task<PortableCoachPackImportResult> ExecuteAsync(
        PortableCoachPackImportPreview approvedPreview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        if (approvedPreview.MatchupPreview is not null)
        {
            var result = await _importMatchup.ExecuteAsync(
                approvedPreview.MatchupPreview,
                cancellationToken);
            return new PortableCoachPackImportResult(
                result.Succeeded,
                PortableCoachPackImportPreview.FromMatchup(result.Preview));
        }

        if (approvedPreview.MapControlPreview is not null)
        {
            var result = await _importMapControl.ExecuteAsync(
                approvedPreview.MapControlPreview,
                cancellationToken);
            return new PortableCoachPackImportResult(
                result.Succeeded,
                PortableCoachPackImportPreview.FromMapControl(result.Preview));
        }

        throw new InvalidDataException("Portable Coach Pack preview has no supported pack.");
    }

    private static string? GetPackType(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Coach Pack root must be a JSON object.");
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals("packType", StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : throw new InvalidDataException("Coach Pack type must be a string.");
            }
        }

        return null;
    }
}
