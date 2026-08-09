namespace TempoCue.App.Tests;

/// <summary>
/// Тесты локализации меняют общий язык и культуру процесса, поэтому не должны
/// выполняться параллельно друг с другом.
/// </summary>
[CollectionDefinition("Локализация")]
public sealed class LocalizationCollection
{
    public const string Name = "Локализация";
}
