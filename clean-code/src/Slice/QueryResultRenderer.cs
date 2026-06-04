using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Slice;

internal static class QueryResultRenderer
{
    public static void Write(QueryResult result, OutputFormat format, Stream output)
    {
        using StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

        switch (result)
        {
            case QueryResult.Scalar scalar:
                WriteScalar(writer, scalar, format);
                break;
            case QueryResult.Table table:
                WriteTable(writer, table, format);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }

        writer.Flush();
        output.Flush();
    }

    private static void WriteScalar(TextWriter writer, QueryResult.Scalar scalar, OutputFormat format)
    {
        switch (format)
        {
            case OutputFormat.Json when TryParseJsonNumber(scalar.Value, out string? jsonNumber):
                writer.Write(jsonNumber);
                break;
            case OutputFormat.Json:
                writer.Write(JsonSerializer.Serialize(scalar.Value));
                break;
            default:
                writer.Write(scalar.Value);
                break;
        }

        writer.Write("\r\n");
    }

    private static void WriteTable(TextWriter writer, QueryResult.Table table, OutputFormat format)
    {
        switch (format)
        {
            case OutputFormat.Csv:
                WriteCsvTable(writer, table);
                break;
            case OutputFormat.Json:
                WriteJsonTable(writer, table);
                break;
            case OutputFormat.Table:
                WriteHumanReadableTable(writer, table);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    private static void WriteCsvTable(TextWriter writer, QueryResult.Table table)
    {
        writer.Write(CsvWriter.FormatRow(table.Header));
        writer.Write("\r\n");

        foreach (IReadOnlyList<string> row in table.Rows)
        {
            writer.Write(CsvWriter.FormatRow(row));
            writer.Write("\r\n");
        }
    }

    private static void WriteJsonTable(TextWriter writer, QueryResult.Table table)
    {
        var rows = new List<Dictionary<string, string>>(table.Rows.Count);
        foreach (IReadOnlyList<string> row in table.Rows)
        {
            Dictionary<string, string> jsonRow = [];
            for (int columnIndex = 0; columnIndex < table.Header.Count; columnIndex++)
            {
                jsonRow[table.Header[columnIndex]] = columnIndex < row.Count ? row[columnIndex] : string.Empty;
            }

            rows.Add(jsonRow);
        }

        writer.Write(JsonSerializer.Serialize(rows));
        writer.Write("\r\n");
    }

    private static void WriteHumanReadableTable(TextWriter writer, QueryResult.Table table)
    {
        int[] columnWidths = GetColumnWidths(table);
        string border = BuildBorder(columnWidths);

        writer.Write(border);
        writer.Write("\r\n");
        writer.Write(BuildRow(table.Header, columnWidths));
        writer.Write("\r\n");
        writer.Write(border);
        writer.Write("\r\n");

        foreach (IReadOnlyList<string> row in table.Rows)
        {
            writer.Write(BuildRow(row, columnWidths));
            writer.Write("\r\n");
        }

        if (table.Rows.Count > 0)
        {
            writer.Write(border);
            writer.Write("\r\n");
        }
    }

    private static int[] GetColumnWidths(QueryResult.Table table)
    {
        int[] widths = new int[table.Header.Count];
        for (int columnIndex = 0; columnIndex < table.Header.Count; columnIndex++)
        {
            widths[columnIndex] = table.Header[columnIndex].Length;
        }

        foreach (IReadOnlyList<string> row in table.Rows)
        {
            for (int columnIndex = 0; columnIndex < table.Header.Count; columnIndex++)
            {
                string value = columnIndex < row.Count ? row[columnIndex] : string.Empty;
                widths[columnIndex] = Math.Max(widths[columnIndex], value.Length);
            }
        }

        return widths;
    }

    private static string BuildBorder(IReadOnlyList<int> columnWidths)
    {
        var builder = new StringBuilder();
        builder.Append('+');
        foreach (int width in columnWidths)
        {
            builder.Append(new string('-', width + 2));
            builder.Append('+');
        }

        return builder.ToString();
    }

    private static string BuildRow(IReadOnlyList<string> values, IReadOnlyList<int> columnWidths)
    {
        var builder = new StringBuilder();
        builder.Append('|');
        for (int columnIndex = 0; columnIndex < columnWidths.Count; columnIndex++)
        {
            string value = columnIndex < values.Count ? values[columnIndex] : string.Empty;
            builder.Append(' ');
            builder.Append(value.PadRight(columnWidths[columnIndex]));
            builder.Append(' ');
            builder.Append('|');
        }

        return builder.ToString();
    }

    private static bool TryParseJsonNumber(string value, out string? jsonNumber)
    {
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number))
        {
            jsonNumber = number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        jsonNumber = null;
        return false;
    }
}
