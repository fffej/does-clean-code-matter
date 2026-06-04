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

        if (!CsvQueryOperations.TryReadInitialTable(csvPath, error, out QueryResult.Table table))
        {
            return null;
        }

        return CsvQueryOperations.TryApplyHead(table, rowCount, out QueryResult.Table result)
            ? result
            : null;
    }

    private static bool TryParseRowCount(string rowCountArgument, out int rowCount)
    {
        return int.TryParse(rowCountArgument, out rowCount) && rowCount > 0;
    }
}
