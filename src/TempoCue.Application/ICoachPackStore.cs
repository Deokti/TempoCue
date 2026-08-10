using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Внешняя граница локального хранения пользовательских Coach Packs.</summary>
public interface ICoachPackStore
{
    Task<CoachPackSaveResult> SaveAsync(
        CoachPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default);
}
