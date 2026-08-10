using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Внешняя граница локального хранения пользовательских Coach Packs.</summary>
public interface ICoachPackStore
{
    Task<CoachPackLibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default);

    Task<CoachPackSaveResult> SaveAsync(
        CoachPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default);

    Task SetStateAsync(
        string packId,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default);
}
