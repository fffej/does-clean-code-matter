namespace Slice;

public static class CsvHeadApp
{
    public static QueryResult? Run(string csvPath, string rowCountArgument, TextWriter error)
    {
        if (!TryParseRowCount(rowCountArgument, out int rowCount))
        {
            error.WriteLine($"Invalid head count: {rowCountArgument}");
            return null;
        }

        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return null;
        }

        return new QueryResult.Table(rows[0], GetRows(rows, rowCount));
    }

    private static bool TryParseRowCount(string rowCountArgument, out int rowCount)
    {
        return int.TryParse(rowCountArgument, out rowCount) && rowCount > 0;
    }

    private static IReadOnlyList<IReadOnlyList<string>> GetRows(IReadOnlyList<IReadOnlyList<string>> rows, int rowCount)
    {
        int rowsToWrite = Math.Min(rows.Count, rowCount + 1);
        List<IReadOnlyList<string>> selectedRows = [];
        for (int rowIndex = 0; rowIndex < rowsToWrite; rowIndex++)
        {
            if (rowIndex == 0)
            {
                continue;
            }

            selectedRows.Add(rows[rowIndex]);
        }

        return selectedRows;
    }
}
