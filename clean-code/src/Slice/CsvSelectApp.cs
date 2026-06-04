namespace Slice;

public static class CsvSelectApp
{
    public static QueryResult? Run(string csvPath, string selectedColumnsArgument, TextWriter error)
    {
        IReadOnlyList<string> selectedColumns = ParseSelectedColumns(selectedColumnsArgument, error);
        if (selectedColumns.Count == 0)
        {
            return null;
        }

        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return null;
        }

        IReadOnlyList<string> header = rows[0];
        int[] columnIndexes = ResolveColumnIndexes(header, selectedColumns, error);
        if (columnIndexes.Length == 0)
        {
            return null;
        }

        return new QueryResult.Table(GetSelectedValues(header, columnIndexes), GetSelectedRows(rows, columnIndexes));
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

    private static IReadOnlyList<IReadOnlyList<string>> GetSelectedRows(
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<int> columnIndexes)
    {
        List<IReadOnlyList<string>> selectedRows = [];
        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            IReadOnlyList<string> row = rows[rowIndex];
            selectedRows.Add(GetSelectedValues(row, columnIndexes));
        }

        return selectedRows;
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
}
