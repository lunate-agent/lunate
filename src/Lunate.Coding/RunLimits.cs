using System.Globalization;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// Parsing and display for the cost and wall-clock run limits. Both are off when unset; a set
/// value must be positive. Durations accept integer seconds or a <c>s</c>/<c>m</c>/<c>h</c> suffix.
/// </summary>
internal static class RunLimits
{
    internal static bool TryParseCost(string? value, out decimal cost)
    {
        cost = 0m;
        return value is not null
            && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out cost)
            && cost > 0m;
    }

    internal static bool TryParseTime(string? value, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string text = value.Trim();
        decimal multiplier = 1m;
        if (text.EndsWith('s'))
        {
            text = text[..^1];
        }
        else if (text.EndsWith('m'))
        {
            text = text[..^1];
            multiplier = 60m;
        }
        else if (text.EndsWith('h'))
        {
            text = text[..^1];
            multiplier = 3_600m;
        }

        if (
            !decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal seconds
            )
            || seconds <= 0m
        )
        {
            return false;
        }

        decimal total = seconds * multiplier;
        if (total > (decimal)TimeSpan.MaxValue.TotalSeconds)
        {
            return false;
        }

        time = TimeSpan.FromSeconds((double)total);
        return true;
    }

    /// <summary>Formats a duration culture-invariantly in seconds, with up to three decimals.</summary>
    internal static string FormatSeconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";

    /// <summary>Formats a US-dollar amount with the product's existing cost formatter.</summary>
    internal static string FormatCost(decimal value) => CostCalculator.Format(value);
}
