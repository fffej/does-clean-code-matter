namespace Slice;

public static class CsvWhereApp
{
    public static QueryResult? Run(string csvPath, string filterExpression, TextWriter error)
    {
        if (!CsvQueryOperations.TryReadInitialTable(csvPath, error, out QueryResult.Table table))
        {
            return null;
        }

        return CsvQueryOperations.TryApplyWhere(table, filterExpression, error, out QueryResult.Table result)
            ? result
            : null;
    }
}
