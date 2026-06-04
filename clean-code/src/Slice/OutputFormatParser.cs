namespace Slice;

internal static class OutputFormatParser
{
    public static bool TryParse(string value, out OutputFormat format)
    {
        format = value.Trim().ToLowerInvariant() switch
        {
            "csv" => OutputFormat.Csv,
            "json" => OutputFormat.Json,
            "table" => OutputFormat.Table,
            _ => default
        };

        return format is OutputFormat.Csv or OutputFormat.Json or OutputFormat.Table;
    }
}
