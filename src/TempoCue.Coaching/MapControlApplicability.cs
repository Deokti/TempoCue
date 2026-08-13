namespace TempoCue.Coaching;

/// <summary>Ограничения, при которых набор контроля карты может применяться в матче.</summary>
public sealed record MapControlApplicability
{
    /// <summary>Роли активного игрока, для которых предназначен набор.</summary>
    public IReadOnlyList<MapControlRole> Roles { get; init; } = MapControlRoleValues.All;

    /// <summary>Сторона активного игрока без привязки к представлению Riot API.</summary>
    public MapControlTeamSide TeamSide { get; init; } = MapControlTeamSide.Any;
}

/// <summary>Роль активного игрока, ограничивающая применимость набора.</summary>
public enum MapControlRole
{
    Top,
    Jungle,
    Mid,
    Adc,
    Support
}

/// <summary>Стабильный перечень поддерживаемых ролей для значений по умолчанию и проверки данных.</summary>
public static class MapControlRoleValues
{
    public static IReadOnlyList<MapControlRole> All { get; } = Array.AsReadOnly(
    new[]
    {
        MapControlRole.Top,
        MapControlRole.Jungle,
        MapControlRole.Mid,
        MapControlRole.Adc,
        MapControlRole.Support
    });
}

/// <summary>Сторона команды активного игрока, ограничивающая применимость набора.</summary>
public enum MapControlTeamSide
{
    Any,
    Blue,
    Red
}
