namespace TempoCue.Application;

/// <summary>Проверяет и записывает переносимый Map Control Pack в выбранный поток.</summary>
public sealed class ExportMapControlPackUseCase
{
    public Task ExecuteAsync(
        MapControlPack pack,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(destination);
        return MapControlPackWriter.WriteAsync(destination, pack, cancellationToken);
    }
}
