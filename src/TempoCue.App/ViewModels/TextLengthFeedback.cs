using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Понятное пользователю состояние ограничения длины текстового поля.</summary>
public sealed record TextLengthFeedback(string Text, bool HasError)
{
    public static TextLengthFeedback Create(
        string value,
        int minimumLength,
        int maximumLength,
        LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(localization);

        var length = value.Trim().Length;
        if (value.Length == 0)
        {
            return new TextLengthFeedback(
                localization.Format(
                    "Validation.Length.Requirement",
                    minimumLength,
                    maximumLength),
                HasError: false);
        }

        if (length < minimumLength)
        {
            return new TextLengthFeedback(
                localization.Format(
                    "Validation.Length.TooShort",
                    length,
                    minimumLength,
                    maximumLength),
                HasError: true);
        }

        if (length > maximumLength)
        {
            return new TextLengthFeedback(
                localization.Format(
                    "Validation.Length.TooLong",
                    length,
                    maximumLength),
                HasError: true);
        }

        return new TextLengthFeedback(
            localization.Format(
                "Validation.Length.Valid",
                length,
                maximumLength),
            HasError: false);
    }

    public static bool IsWithin(string value, int minimumLength, int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        var length = value.Trim().Length;
        return length >= minimumLength && length <= maximumLength;
    }
}
