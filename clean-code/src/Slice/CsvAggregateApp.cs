using System.Globalization;
using System.Text;

namespace Slice;

public static class CsvAggregateApp
{
    public static int RunCount(string csvPath, Stream output, TextWriter error)
    {
        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return 1;
        }

        using StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(rows.Count - 1);
        writer.Write("\r\n");
        writer.Flush();
        output.Flush();
        return 0;
    }

    public static int RunSum(string csvPath, string columnName, Stream output, TextWriter error)
    {
        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return 1;
        }

        IReadOnlyList<string> header = rows[0];
        int columnIndex = CsvHeaderLookup.FindHeaderIndex(header, columnName);
        if (columnIndex < 0)
        {
            error.WriteLine($"Column not found: {columnName}");
            return 1;
        }

        if (!TrySumColumn(rows, columnIndex, out decimal sum, out string? invalidValue))
        {
            error.WriteLine($"Non-numeric value found in column {columnName}: {invalidValue}");
            return 1;
        }

        using StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(sum.ToString("G29", CultureInfo.InvariantCulture));
        writer.Write("\r\n");
        writer.Flush();
        output.Flush();
        return 0;
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
