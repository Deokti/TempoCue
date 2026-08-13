using System.Globalization;
using TempoCue.App.Localization;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Единообразно форматирует общие метаданные разных видов Coach Pack.</summary>
internal static class CoachPackDetailsPresentation
{
    internal static string Description(LocalizationService localization, string? description) =>
        string.IsNullOrWhiteSpace(description)
            ? localization.Get("Coaching.Details.Overview.Description.Empty")
            : description;

    internal static string Author(LocalizationService localization, string? author) =>
        string.IsNullOrWhiteSpace(author)
            ? localization.Get("Coaching.Library.Author.TempoCue")
            : author;

    internal static string VerifiedOn(LocalizationService localization, DateOnly? verifiedOn) =>
        verifiedOn is { } value
            ? value.ToString("D", CultureInfo.CurrentUICulture)
            : localization.Get("Coaching.Details.Overview.NotSpecified");

    internal static string SourceLabel(
        LocalizationService localization,
        CoachPackSource? source,
        string? fallback = null)
    {
        if (source is null)
        {
            return string.IsNullOrWhiteSpace(fallback)
                ? localization.Get("Coaching.Details.Overview.NotSpecified")
                : fallback;
        }

        if (source.Kind == CoachPackSourceKind.PersonalExperience)
        {
            return localization.Get("Coaching.Details.Source.PersonalExperience");
        }

        var title = string.IsNullOrWhiteSpace(source.Title)
            ? localization.Get("Coaching.Details.Overview.NotSpecified")
            : source.Title;
        return string.IsNullOrWhiteSpace(source.Author)
            ? title
            : $"{title} — {source.Author}";
    }

    internal static Uri? SourceUri(CoachPackSource? source)
    {
        if (!Uri.TryCreate(source?.Url, UriKind.Absolute, out var sourceUri))
        {
            return null;
        }

        return sourceUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || sourceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? sourceUri
            : null;
    }
}
