using System.Text.RegularExpressions;

namespace ProcessLogger.Core.Query;

public enum TextFilterMode
{
    PartialText,
    ExactString,
    Regex,
}

public sealed record TextFilter(string? Text, TextFilterMode Mode = TextFilterMode.PartialText);

public static class TextFilterExtensions
{
    public static bool IsSet(this TextFilter? filter) => !string.IsNullOrWhiteSpace(filter?.Text);

    public static bool Matches(this TextFilter? filter, string? value)
    {
        if (!filter.IsSet() || value is null)
        {
            return false;
        }

        return filter!.Mode switch
        {
            TextFilterMode.ExactString => string.Equals(value, filter.Text, StringComparison.OrdinalIgnoreCase),
            TextFilterMode.Regex => Regex.IsMatch(value, filter.Text!, RegexOptions.IgnoreCase),
            _ => value.Contains(filter.Text!, StringComparison.OrdinalIgnoreCase),
        };
    }
}
