namespace TempoCue.Coaching;

/// <summary>Общие проверки метаданных для переносимых наборов коучинга.</summary>
internal static class CoachPackValidation
{
    internal static void ValidateSource(CoachPackSource source)
    {
        if (!Enum.IsDefined(source.Kind) || !Enum.IsDefined(source.Permission))
        {
            throw new InvalidDataException("Coach Pack source contains an unsupported value.");
        }

        if (source.Kind == CoachPackSourceKind.PersonalExperience)
        {
            if (source.Permission != CoachPackSourcePermission.NotRequired)
            {
                throw new InvalidDataException("Personal experience does not require external permission.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(source.Title)
            || string.IsNullOrWhiteSpace(source.Author)
            || !IsOptionalHttpUrl(source.Url))
        {
            throw new InvalidDataException(
                "External source requires title and author; an optional URL must use HTTP(S).");
        }
    }

    private static bool IsOptionalHttpUrl(string value) =>
        string.IsNullOrWhiteSpace(value)
        || Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    internal static void EnsureUniqueIds(IEnumerable<string> ids, string entityName)
    {
        var duplicate = ids
            .GroupBy(static id => id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Invalid or duplicate {entityName} id: {duplicate.Key}.");
        }
    }
}
