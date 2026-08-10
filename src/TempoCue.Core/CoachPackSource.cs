namespace TempoCue.Core;

/// <summary>Происхождение рекомендаций одного Coach Pack.</summary>
public sealed record CoachPackSource
{
    public CoachPackSourceKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public CoachPackSourcePermission Permission { get; init; }
}
