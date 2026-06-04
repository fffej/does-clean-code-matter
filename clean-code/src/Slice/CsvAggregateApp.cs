using System.Globalization;

namespace Slice;

public static class CsvAggregateApp
{
    public static QueryResult? RunCount(string csvPath, TextWriter error)
    {
        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return null;
        }

        return new QueryResult.Scalar((rows.Count - 1).ToString(CultureInfo.InvariantCulture));
    }

    public static QueryResult? RunSum(string csvPath, string columnName, TextWriter error)
    {
        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return null;
        }

        IReadOnlyList<string> header = rows[0];
        int columnIndex = CsvHeaderLookup.FindHeaderIndex(header, columnName);
        if (columnIndex < 0)
        {
            error.WriteLine($"Column not found: {columnName}");
            return null;
        }

        if (!TrySumColumn(rows, columnIndex, out decimal sum, out string? invalidValue))
        {
            error.WriteLine($"Non-numeric value found in column {columnName}: {invalidValue}");
            return null;
        }

        return new QueryResult.Scalar(sum.ToString("G29", CultureInfo.InvariantCulture));
    }

    private static bool TrySumColumn(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int columnIndex,
        out decimal sum,
        out string? invalidValue)
    {
        sum = 0m;
        invalidValue = null;

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            IReadOnlyList<string> row = rows[rowIndex];
            string value = columnIndex < row.Count ? row[columnIndex] : string.Empty;
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsedValue))
            {
                invalidValue = value;
                return false;
            }

            sum += parsedValue;
        }

        return true;
    }
}
