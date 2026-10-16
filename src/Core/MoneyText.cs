using System.Globalization;

namespace Mc.Scripting.Essentials.Core;

/// <summary>Reads and writes amounts as text without floating point: <c>12</c>, <c>12.5</c> and <c>12.50</c> are the same kind of input.</summary>
public static class MoneyText
{
    /// <summary>Parses an amount into minor units.</summary>
    /// <param name="text">The text typed by a player: digits, at most one point, no sign.</param>
    /// <param name="fractionDigits">Minor units per major unit as a power of ten (2 = cents).</param>
    /// <param name="minor">The amount in minor units when the text is valid.</param>
    /// <returns>False for an empty value, a sign, letters, more fraction digits than the currency has, or overflow.</returns>
    public static bool TryParse(string? text, int fractionDigits, out long minor)
    {
        minor = 0;
        if (string.IsNullOrEmpty(text) || fractionDigits is < 0 or > 9) return false;

        int dot = text.IndexOf('.', StringComparison.Ordinal);
        string whole = dot < 0 ? text : text[..dot];
        string fraction = dot < 0 ? "" : text[(dot + 1)..];
        if (whole.Length == 0 || !IsDigits(whole)) return false;
        if (dot >= 0 && (fraction.Length == 0 || fraction.Length > fractionDigits || !IsDigits(fraction))) return false;

        long scale = 1;
        for (int i = 0; i < fractionDigits; i++) scale *= 10;
        if (!long.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out long majorUnits)) return false;

        long minorUnits = 0;
        if (fraction.Length > 0)
        {
            minorUnits = long.Parse(fraction.PadRight(fractionDigits, '0'), NumberStyles.None, CultureInfo.InvariantCulture);
        }
        if (majorUnits > (long.MaxValue - minorUnits) / scale) return false;

        minor = majorUnits * scale + minorUnits;
        return true;
    }

    private static bool IsDigits(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }
}
