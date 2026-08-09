namespace TempoCue.App;

/// <summary>Предоставляет номер собранной версии для интерфейса и диагностики.</summary>
public static class AppVersion
{
    private static readonly Version AssemblyVersion =
        typeof(AppVersion).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static string Display =>
        $"v{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{Math.Max(0, AssemblyVersion.Build)}";
}
