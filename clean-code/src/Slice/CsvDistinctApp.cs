namespace Slice;

public static class CsvDistinctApp
{
    public static QueryResult? Run(string csvPath, string[] distinctColumnsArguments, TextWriter error)
    {
        if (!CsvQueryOperations.TryReadInitialTable(csvPath, error, out QueryResult.Table table))
        {
            return null;
        }

        return CsvQueryOperations.TryApplyDistinct(table, distinctColumnsArguments, error, out QueryResult.Table result)
            ? result
            : null;
    }
}
