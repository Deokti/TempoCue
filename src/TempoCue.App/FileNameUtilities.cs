using System.IO;

namespace TempoCue.App;

/// <summary>Создаёт безопасные имена для файлов, которые пользователь сохраняет из приложения.</summary>
internal static class FileNameUtilities
{
    public static string Create(string title, string extension, string fallbackBaseName)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackBaseName);

        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var safeName = new string(title
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray())
            .Trim();
        var baseName = string.IsNullOrWhiteSpace(safeName)
            ? fallbackBaseName
            : safeName;
        return $"{baseName}{extension}";
    }
}
