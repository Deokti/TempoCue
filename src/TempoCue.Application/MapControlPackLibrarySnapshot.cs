namespace TempoCue.Application;

/// <summary>Результат чтения локальной библиотеки контроля карты.</summary>
public sealed record MapControlPackLibrarySnapshot(
    IReadOnlyList<LocalMapControlPackEntry> Entries,
    int InvalidFileCount,
    IReadOnlyDictionary<string, LocalCoachPackState> PackStates);
