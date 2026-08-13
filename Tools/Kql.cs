using System.Globalization;
using System.Text.RegularExpressions;

namespace AppInsightsMcp.Tools;

/// <summary>
/// Small helpers shared by the tool wrappers: optional where-clauses and human time ranges.
/// </summary>
internal static partial class Kql
{
    /// <summary>Renders an optional KQL where-clause, or an empty line when no filter was given.</summary>
    public static string Where(string? filter) =>
        string.IsNullOrWhiteSpace(filter) ? "" : $"| where {filter}";

    /// <summary>
    /// Parses a duration like "7d", "24h", "90m", "30s" (or a bare number of minutes) into a TimeSpan.
    /// </summary>
    public static TimeSpan Range(string? timeRange)
    {
        if (string.IsNullOrWhiteSpace(timeRange))
            return TimeSpan.FromHours(1);

        var m = DurationPattern().Match(timeRange.Trim());
        if (!m.Success)
            throw new ArgumentException($"Invalid timeRange '{timeRange}'. Use e.g. '30m', '12h', '7d' or a bare number of minutes.");

        var value = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        return m.Groups[2].Value.ToLowerInvariant() switch
        {
            "s" => TimeSpan.FromSeconds(value),
            "h" => TimeSpan.FromHours(value),
            "d" => TimeSpan.FromDays(value),
            _ => TimeSpan.FromMinutes(value), // bare number or "m"
        };
    }

    [GeneratedRegex(@"^(\d+(?:\.\d+)?)\s*([smhd]?)$", RegexOptions.IgnoreCase)]
    private static partial Regex DurationPattern();

    /// <summary>Runnable check: dotnet run -- --selftest</summary>
    public static void SelfTest()
    {
        Check(Range("90") == TimeSpan.FromMinutes(90), "bare number = minutes");
        Check(Range("30m") == TimeSpan.FromMinutes(30), "m");
        Check(Range("24h") == TimeSpan.FromHours(24), "h");
        Check(Range("7d") == TimeSpan.FromDays(7), "d");
        Check(Range("1.5h") == TimeSpan.FromMinutes(90), "fractional");
        Check(Range(" 7D ") == TimeSpan.FromDays(7), "trim + case");
        Check(Range(null) == TimeSpan.FromHours(1), "default");
        Check(Throws(() => Range("last week")), "garbage rejected");
        Check(Where(null) == "", "no filter");
        Check(Where("  ") == "", "blank filter");
        Check(Where("Name has 'api'") == "| where Name has 'api'", "filter");
        Console.WriteLine("Kql selftest OK");
    }

    private static bool Throws(Action a)
    {
        try { a(); return false; } catch (ArgumentException) { return true; }
    }

    private static void Check(bool ok, string what)
    {
        if (!ok) throw new Exception($"selftest failed: {what}");
    }
}
