using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Slice;

internal enum OutputFormat
{
    Csv,
    Json,
    Table
}

internal static class OutputRenderer
{
    public static async Task RenderAsync(Stream output, QueryResult result, OutputFormat format)
    {
        switch (format)
        {
            case OutputFormat.Csv:
                await RenderCsvAsync(output, result).ConfigureAwait(false);
                return;
            case OutputFormat.Json:
                await RenderJsonAsync(output, result).ConfigureAwait(false);
                return;
            case OutputFormat.Table:
                await RenderTableAsync(output, result).ConfigureAwait(false);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    private static async Task RenderCsvAsync(Stream output, QueryResult result)
    {
        await using var writer = CreateWriter(output);

        switch (result)
        {
            case TableQueryResult tableResult:
                await writer.WriteLineAsync(BuildCsvRow(tableResult.Headers)).ConfigureAwait(false);
                foreach (var row in tableResult.Rows)
                {
                    await writer.WriteLineAsync(BuildCsvRow(row)).ConfigureAwait(false);
                }
                break;
            case ScalarQueryResult scalarResult:
                await writer.WriteLineAsync(FormatDecimal(scalarResult.Value)).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported result type: {result.GetType().Name}");
        }

        await writer.FlushAsync().ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    private static async Task RenderJsonAsync(Stream output, QueryResult result)
    {
        using var jsonWriter = new Utf8JsonWriter(output, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        });

        switch (result)
        {
            case TableQueryResult tableResult:
                jsonWriter.WriteStartArray();
                foreach (var row in tableResult.Rows)
                {
                    jsonWriter.WriteStartObject();
                    for (var i = 0; i < tableResult.Headers.Count; i++)
                    {
                        var header = tableResult.Headers[i];
                        var value = i < row.Count ? row[i] : string.Empty;
                        jsonWriter.WriteString(header, value);
                    }
                    jsonWriter.WriteEndObject();
                }
                jsonWriter.WriteEndArray();
                break;
            case ScalarQueryResult scalarResult:
                jsonWriter.WriteNumberValue(scalarResult.Value);
                break;
            default:
                throw new InvalidOperationException($"Unsupported result type: {result.GetType().Name}");
        }

        await jsonWriter.FlushAsync().ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    private static async Task RenderTableAsync(Stream output, QueryResult result)
    {
        await using var writer = CreateWriter(output);

        switch (result)
        {
            case TableQueryResult tableResult:
                var widths = GetColumnWidths(tableResult.Headers, tableResult.Rows);
                await writer.WriteLineAsync(BuildTableRow(tableResult.Headers, widths)).ConfigureAwait(false);
                await writer.WriteLineAsync(BuildTableSeparator(widths)).ConfigureAwait(false);
                foreach (var row in tableResult.Rows)
                {
                    await writer.WriteLineAsync(BuildTableRow(row, widths)).ConfigureAwait(false);
                }
                break;
            case ScalarQueryResult scalarResult:
                await writer.WriteLineAsync(FormatDecimal(scalarResult.Value)).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported result type: {result.GetType().Name}");
        }

        await writer.FlushAsync().ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    private static string BuildCsvRow(IReadOnlyList<string> fields)
    {
        var outputFields = new string[fields.Count];
        for (var i = 0; i < fields.Count; i++)
        {
            outputFields[i] = EscapeCsvField(fields[i]);
        }

        return string.Join(",", outputFields);
    }

    private static string BuildTableRow(IReadOnlyList<string> values, int[] widths)
    {
        var cells = new string[widths.Length];
        for (var i = 0; i < widths.Length; i++)
        {
            var value = i < values.Count ? values[i] : string.Empty;
            cells[i] = value.PadRight(widths[i]);
        }

        return string.Join(" | ", cells);
    }

    private static string BuildTableSeparator(int[] widths)
    {
        return string.Join("-+-", widths.Select(width => new string('-', width)));
    }

    private static int[] GetColumnWidths(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var widths = headers.Select(header => header.Length).ToArray();
        foreach (var row in rows)
        {
            for (var i = 0; i < widths.Length; i++)
            {
                var valueLength = i < row.Count ? row[i].Length : 0;
                if (valueLength > widths[i])
                {
                    widths[i] = valueLength;
                }
            }
        }

        return widths;
    }

    private static StreamWriter CreateWriter(Stream output)
    {
        return new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            NewLine = "\r\n"
        };
    }

    private static string EscapeCsvField(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var requiresQuoting = value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\r') ||
            value.Contains('\n');

        if (!requiresQuoting)
        {
            return value;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("0.#############################", CultureInfo.InvariantCulture);
    }
}
