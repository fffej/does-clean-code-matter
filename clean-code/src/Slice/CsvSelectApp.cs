namespace Slice;

public static class CsvSelectApp
{
    public static QueryResult? Run(string csvPath, string selectedColumnsArgument, TextWriter error)
    {
        if (!CsvQueryOperations.TryReadInitialTable(csvPath, error, out QueryResult.Table table))
        {
            return null;
        }

        return CsvQueryOperations.TryApplySelect(table, selectedColumnsArgument, error, out QueryResult.Table result)
            ? result
            : null;
    }
}
