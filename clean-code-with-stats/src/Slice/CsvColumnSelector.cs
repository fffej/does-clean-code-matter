using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace Slice;

internal sealed class CsvColumnSelector
{
    public IReadOnlyList<string> ParseRequestedColumns(string columnsArgument)
    {
        if (string.IsNullOrWhiteSpace(columnsArgument))
        {
            return Array.Empty<string>();
        }

        return columnsArgument
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<string?> WriteSelectedColumnsAsync(
        Stream input,
        Stream output,
        IReadOnlyList<string> requestedColumns)
    {
        if (requestedColumns.Count == 0)
        {
            return "No columns were selected.";
        }

        using var parser = new TextFieldParser(input, Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };

        parser.SetDelimiters(",");

        var headers = parser.ReadFields();
        if (headers is null)
        {
            return "CSV file is empty.";
        }

        var selectedIndexes = new int[requestedColumns.Count];
        for (var i = 0; i < requestedColumns.Count; i++)
        {
            var requestedColumn = requestedColumns[i];
            var index = Array.FindIndex(headers, header => string.Equals(header, requestedColumn, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return $"Column not found: {requestedColumn}";
            }

            selectedIndexes[i] = index;
        }

        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            NewLine = "\r\n"
        };

        await writer.WriteLineAsync(BuildCsvRow(headers, selectedIndexes)).ConfigureAwait(false);

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            await writer.WriteLineAsync(BuildCsvRow(fields, selectedIndexes)).ConfigureAwait(false);
        }

        await writer.FlushAsync().ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return null;
    }

    private static string BuildCsvRow(IReadOnlyList<string> fields, IReadOnlyList<int> selectedIndexes)
    {
        var selectedFields = new string[selectedIndexes.Count];
        for (var i = 0; i < selectedIndexes.Count; i++)
        {
            var selectedIndex = selectedIndexes[i];
            selectedFields[i] = selectedIndex < fields.Count ? EscapeCsvField(fields[selectedIndex]) : string.Empty;
        }

        return string.Join(",", selectedFields);
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
}
