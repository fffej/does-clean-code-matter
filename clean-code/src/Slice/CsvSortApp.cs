namespace Slice;

public static class CsvSortApp
{
    public static QueryResult? Run(string csvPath, string sortColumn, string sortDirection, TextWriter error)
    {
        if (!CsvQueryOperations.TryReadInitialTable(csvPath, error, out QueryResult.Table table))
        {
            return null;
        }

        return CsvQueryOperations.TryApplySort(table, sortColumn, sortDirection, error, out QueryResult.Table result)
            ? result
            : null;
    }
}
