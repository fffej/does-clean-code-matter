namespace Slice;

public static class CsvGroupByApp
{
    public static QueryResult? Run(string csvPath, string groupColumn, string[] aggregateArguments, TextWriter error)
    {
        if (!CsvQueryOperations.TryReadInitialTable(csvPath, error, out QueryResult.Table table))
        {
            return null;
        }

        return CsvQueryOperations.TryApplyGroupBy(table, groupColumn, aggregateArguments, error, out QueryResult.Table result)
            ? result
            : null;
    }
}
