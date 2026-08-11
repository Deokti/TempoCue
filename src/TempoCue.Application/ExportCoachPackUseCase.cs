using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Проверяет и записывает переносимый Coach Pack в выбранный пользователем поток.</summary>
public sealed class ExportCoachPackUseCase
{
    public Task ExecuteAsync(
        CoachPack pack,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(destination);
        return CoachPackWriter.WriteAsync(destination, pack, cancellationToken);
    }
}
