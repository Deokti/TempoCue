using System.Text.Json;

namespace TempoCue.Vision.DeeperLeague;

/// <summary>
/// Отображает индекс класса модели в идентификатор чемпиона. Имена в карте модели
/// совпадают с идентификаторами Data Dragon, поэтому преобразование прямое.
/// </summary>
public sealed class DeeperLeagueChampionMap
{
    private readonly string[] _championIds;

    private DeeperLeagueChampionMap(string[] championIds) => _championIds = championIds;

    public int Count => _championIds.Length;

    public static DeeperLeagueChampionMap Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Карта чемпионов модели не найдена: {path}");
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var entries = new SortedDictionary<int, string>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!int.TryParse(property.Name, out var index))
            {
                continue;
            }

            var name = property.Value.TryGetProperty("champ_name", out var championName)
                ? championName.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                entries[index] = name;
            }
        }

        if (entries.Count == 0)
        {
            throw new InvalidDataException("Карта чемпионов модели пуста.");
        }

        var championIds = new string[entries.Keys.Max() + 1];
        foreach (var (index, name) in entries)
        {
            championIds[index] = name;
        }

        return new DeeperLeagueChampionMap(championIds);
    }

    /// <summary>Возвращает идентификатор чемпиона или null для неизвестного индекса.</summary>
    public string? this[int classIndex] =>
        classIndex >= 0 && classIndex < _championIds.Length
            ? _championIds[classIndex]
            : null;
}
