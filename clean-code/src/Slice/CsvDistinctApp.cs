using System.Text;

namespace Slice;

public static class CsvDistinctApp
{
    public static int Run(string csvPath, string[] distinctColumnsArguments, Stream output, TextWriter error)
    {
        IReadOnlyList<string> distinctColumns = ParseDistinctColumns(distinctColumnsArguments, error);
        if (distinctColumns.Count == 0)
        {
            return 1;
        }

        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return 1;
        }

        IReadOnlyList<string> header = rows[0];
        int[] columnIndexes = ResolveColumnIndexes(header, distinctColumns, error);
        if (columnIndexes.Length == 0)
        {
            return 1;
        }

        using StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        WriteHeader(writer, header, columnIndexes);
        WriteDistinctRows(writer, rows, columnIndexes);
        writer.Flush();
        output.Flush();
        return 0;
    }

    private static IReadOnlyList<string> ParseDistinctColumns(string[] distinctColumnsArguments, TextWriter error)
    {
        List<string> distinctColumns = [];

        foreach (string argument in distinctColumnsArguments)
        {
            string[] tokens = argument.Split(',', StringSplitOptions.TrimEntries);
            foreach (string token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    error.WriteLine("No columns were specified.");
                    return Array.Empty<string>();
                }

                distinctColumns.Add(token);
            }
        }

        if (distinctColumns.Count == 0)
        {
            error.WriteLine("No columns were specified.");
            return Array.Empty<string>();
        }

        return distinctColumns;
    }

    private static int[] ResolveColumnIndexes(
        IReadOnlyList<string> header,
        IReadOnlyList<string> distinctColumns,
        TextWriter error)
    {
        int[] indexes = new int[distinctColumns.Count];
        for (int columnIndex = 0; columnIndex < distinctColumns.Count; columnIndex++)
        {
            string distinctColumn = distinctColumns[columnIndex];
            int headerIndex = CsvHeaderLookup.FindHeaderIndex(header, distinctColumn);
            if (headerIndex < 0)
            {
                error.WriteLine($"Column not found: {distinctColumn}");
                return Array.Empty<int>();
            }

            indexes[columnIndex] = headerIndex;
        }

        return indexes;
    }

    private static void WriteHeader(TextWriter writer, IReadOnlyList<string> header, IReadOnlyList<int> columnIndexes)
    {
        writer.Write(CsvWriter.FormatRow(GetSelectedValues(header, columnIndexes)));
        writer.Write("\r\n");
    }

    private static void WriteDistinctRows(
        TextWriter writer,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<int> columnIndexes)
    {
        HashSet<string> seenKeys = [];

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            IReadOnlyList<string> row = rows[rowIndex];
            string distinctKey = BuildDistinctKey(row, columnIndexes);
            if (!seenKeys.Add(distinctKey))
            {
                continue;
            }

            writer.Write(CsvWriter.FormatRow(GetSelectedValues(row, columnIndexes)));
            writer.Write("\r\n");
        }
    }

    private static string[] GetSelectedValues(IReadOnlyList<string> row, IReadOnlyList<int> columnIndexes)
    {
        string[] selectedValues = new string[columnIndexes.Count];
        for (int columnIndex = 0; columnIndex < columnIndexes.Count; columnIndex++)
        {
            int sourceIndex = columnIndexes[columnIndex];
            selectedValues[columnIndex] = sourceIndex < row.Count ? row[sourceIndex] : string.Empty;
        }

        return selectedValues;
    }

    private static string BuildDistinctKey(IReadOnlyList<string> row, IReadOnlyList<int> columnIndexes)
    {
        var builder = new StringBuilder();
        for (int columnIndex = 0; columnIndex < columnIndexes.Count; columnIndex++)
        {
            if (columnIndex > 0)
            {
                builder.Append('\u001F');
            }

            string value = GetColumnValue(row, columnIndexes[columnIndex]);
            builder.Append(value.Length);
            builder.Append(':');
            builder.Append(value);
        }

        return builder.ToString();
    }

    private static string GetColumnValue(IReadOnlyList<string> row, int columnIndex)
    {
        return columnIndex < row.Count ? row[columnIndex] : string.Empty;
    }
}
