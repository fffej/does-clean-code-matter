namespace Slice;

public static class CsvAggregateApp
{
    public static QueryResult? RunCount(string csvPath, TextWriter error)
    {
        if (!CsvQueryOperations.TryReadInitialTable(csvPath, error, out QueryResult.Table table))
        {
            return null;
        }

        return CsvQueryOperations.TryApplyCount(table, Array.Empty<string>(), error, out QueryResult result)
            ? result
            : null;
    }

    public static QueryResult? RunSum(string csvPath, string columnName, TextWriter error)
    {
        if (!CsvQueryOperations.TryReadInitialTable(csvPath, error, out QueryResult.Table table))
        {
            return null;
        }

        return CsvQueryOperations.TryApplySum(table, [columnName], error, out QueryResult result)
            ? result
            : null;
    }
}
