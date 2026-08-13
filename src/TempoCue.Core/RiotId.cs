using System.Diagnostics.CodeAnalysis;

namespace TempoCue.Core;

/// <summary>Публичный игровой идентификатор Riot в формате gameName#tagLine.</summary>
public sealed record RiotId
{
    private const int MaximumPartLength = 128;

    private RiotId(string gameName, string tagLine)
    {
        GameName = gameName;
        TagLine = tagLine;
    }

    public string GameName { get; }

    public string TagLine { get; }

    public string DisplayName => $"{GameName}#{TagLine}";

    public static RiotId Create(string gameName, string tagLine)
    {
        if (!TryCreate(gameName, tagLine, out var riotId))
        {
            throw new ArgumentException("Riot ID должен содержать непустые gameName и tagLine.");
        }

        return riotId;
    }

    public static bool TryCreate(
        string? gameName,
        string? tagLine,
        [NotNullWhen(true)] out RiotId? riotId)
    {
        var normalizedGameName = gameName?.Trim();
        var normalizedTagLine = tagLine?.Trim();
        if (!IsValidPart(normalizedGameName) || !IsValidPart(normalizedTagLine))
        {
            riotId = null;
            return false;
        }

        riotId = new RiotId(normalizedGameName, normalizedTagLine);
        return true;
    }

    public override string ToString() => DisplayName;

    private static bool IsValidPart([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaximumPartLength
        && !value.Contains('#', StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
