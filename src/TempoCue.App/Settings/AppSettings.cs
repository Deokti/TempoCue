namespace TempoCue.App.Settings;

/// <summary>Настройки, которые сохраняются между запусками TempoCue.</summary>
public sealed record AppSettings
{
    public string Language { get; init; } = "ru-RU";
}
