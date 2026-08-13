namespace TempoCue.Application;

/// <summary>Внешняя граница локального хранения пользовательских наборов контроля карты.</summary>
public interface IMapControlPackStore
{
    Task<MapControlPackLibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default);

    Task<MapControlPackSaveResult> SaveAsync(
        MapControlPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default);

    Task<MapControlPackSaveResult> ImportAsync(
        MapControlPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default);

    Task SetStateAsync(
        string packId,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string packId,
        CancellationToken cancellationToken = default);
}
