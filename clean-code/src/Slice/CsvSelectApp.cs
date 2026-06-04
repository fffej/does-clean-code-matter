using System.Text;

namespace Slice;

public static class CsvSelectApp
{
    public static int Run(string csvPath, string selectedColumnsArgument, Stream output, TextWriter error)
    {
        if (!File.Exists(csvPath))
        {
            error.WriteLine($"Input file not found: {csvPath}");
            return 1;
        }

        IReadOnlyList<string> selectedColumns = ParseSelectedColumns(selectedColumnsArgument, error);
        if (selectedColumns.Count == 0)
        {
            return 1;
        }

        List<IReadOnlyList<string>> rows;
        try
        {
            using StreamReader reader = File.OpenText(csvPath);
            rows = CsvParser.Parse(reader);
        }
        catch (FormatException exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }

        if (rows.Count == 0)
        {
            error.WriteLine("Input file is empty.");
            return 1;
        }

        IReadOnlyList<string> header = rows[0];
        int[] columnIndexes = ResolveColumnIndexes(header, selectedColumns, error);
        if (columnIndexes.Length == 0)
        {
            return 1;
        }

        using StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
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
            int headerIndex = FindHeaderIndex(header, selectedColumn);
            if (headerIndex < 0)
            {
                error.WriteLine($"Column not found: {selectedColumn}");
                return Array.Empty<int>();
            }

            indexes[columnIndex] = headerIndex;
        }

        return indexes;
    }

    private static int FindHeaderIndex(IReadOnlyList<string> header, string selectedColumn)
    {
        for (int index = 0; index < header.Count; index++)
        {
            if (string.Equals(header[index], selectedColumn, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
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
