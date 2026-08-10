namespace TempoCue.Application;

/// <summary>Результат чтения локальной библиотеки без распространения файловых ошибок в UI.</summary>
public sealed record CoachPackLibrarySnapshot(
    IReadOnlyList<LocalCoachPackEntry> Entries,
    int InvalidFileCount,
    IReadOnlyDictionary<string, LocalCoachPackState> PackStates);
