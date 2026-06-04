namespace Slice;

public static class CsvSelectApp
{
    public static int Run(string csvPath, string selectedColumnsArgument, Stream output, TextWriter error)
    {
        IReadOnlyList<string> selectedColumns = ParseSelectedColumns(selectedColumnsArgument, error);
        if (selectedColumns.Count == 0)
        {
            return 1;
        }

        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return 1;
        }

        IReadOnlyList<string> header = rows[0];
        int[] columnIndexes = ResolveColumnIndexes(header, selectedColumns, error);
        if (columnIndexes.Length == 0)
        {
            return 1;
        }

        using StreamWriter writer = new(output, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        WriteSelectedRows(writer, rows, columnIndexes);
        writer.Flush();
        output.Flush();
        return 0;
    }

    private static IReadOnlyList<string> ParseSelectedColumns(string selectedColumnsArgument, TextWriter error)
    {
        string[] selectedColumns = selectedColumnsArgument.Split(',', StringSplitOptions.TrimEntries);
        if (selectedColumns.Length == 0 || selectedColumns.Any(string.IsNullOrWhiteSpace))
        {
            error.WriteLine("No columns were specified.");
            return Array.Empty<string>();
        }

        return selectedColumns;
    }

    private static int[] ResolveColumnIndexes(
        IReadOnlyList<string> header,
        IReadOnlyList<string> selectedColumns,
        TextWriter error)
    {
        int[] indexes = new int[selectedColumns.Count];
        for (int columnIndex = 0; columnIndex < selectedColumns.Count; columnIndex++)
        {
            string selectedColumn = selectedColumns[columnIndex];
            int headerIndex = CsvHeaderLookup.FindHeaderIndex(header, selectedColumn);
            if (headerIndex < 0)
            {
                error.WriteLine($"Column not found: {selectedColumn}");
                return Array.Empty<int>();
            }

            indexes[columnIndex] = headerIndex;
        }

        return indexes;
    }

    private static void WriteSelectedRows(
        TextWriter writer,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<int> columnIndexes)
    {
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            IReadOnlyList<string> row = rows[rowIndex];
            string[] selectedValues = new string[columnIndexes.Count];

            for (int columnIndex = 0; columnIndex < columnIndexes.Count; columnIndex++)
            {
                int sourceIndex = columnIndexes[columnIndex];
                selectedValues[columnIndex] = sourceIndex < row.Count ? row[sourceIndex] : string.Empty;
            }

            writer.Write(CsvWriter.FormatRow(selectedValues));
            writer.Write("\r\n");
        }
    }
}
