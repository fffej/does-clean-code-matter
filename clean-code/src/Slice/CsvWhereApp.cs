using System.Globalization;
using System.Text;

namespace Slice;

public static class CsvWhereApp
{
    public static int Run(string csvPath, string filterExpression, Stream output, TextWriter error)
    {
        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return 1;
        }

        if (!TryParseFilter(filterExpression, error, out ComparisonFilter filter))
        {
            return 1;
        }

        IReadOnlyList<string> header = rows[0];
        int columnIndex = CsvHeaderLookup.FindHeaderIndex(header, filter.ColumnName);
        if (columnIndex < 0)
        {
            error.WriteLine($"Column not found: {filter.ColumnName}");
            return 1;
        }

        using StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        WriteHeader(writer, header);
        WriteMatchingRows(writer, rows, columnIndex, filter);
        writer.Flush();
        output.Flush();
        return 0;
    }

    private static bool TryParseFilter(string filterExpression, TextWriter error, out ComparisonFilter filter)
    {
        string expression = filterExpression.Trim();
        string[] operatorTokens = ["!=", ">=", "<=", "=", ">", "<"];
        foreach (string operatorToken in operatorTokens)
        {
            int operatorIndex = expression.IndexOf(operatorToken, StringComparison.Ordinal);
            if (operatorIndex <= 0)
            {
                continue;
            }

            string columnName = expression[..operatorIndex].Trim();
            string literalValue = expression[(operatorIndex + operatorToken.Length)..].Trim();
            if (columnName.Length == 0 || literalValue.Length == 0)
            {
                break;
            }

            if (!TryParseComparisonOperator(operatorToken, out ComparisonOperator comparisonOperator))
            {
                break;
            }

            bool literalIsNumeric = TryParseNumber(literalValue, out decimal literalNumericValue);
            filter = new ComparisonFilter(columnName, comparisonOperator, literalValue, literalIsNumeric, literalNumericValue);
            return true;
        }

        error.WriteLine($"Invalid where expression: {filterExpression}");
        filter = default;
        return false;
    }

    private static bool TryParseComparisonOperator(string operatorToken, out ComparisonOperator comparisonOperator)
    {
        comparisonOperator = operatorToken switch
        {
            "=" => ComparisonOperator.Equal,
            "!=" => ComparisonOperator.NotEqual,
            ">" => ComparisonOperator.GreaterThan,
            "<" => ComparisonOperator.LessThan,
            ">=" => ComparisonOperator.GreaterThanOrEqual,
            "<=" => ComparisonOperator.LessThanOrEqual,
            _ => default
        };

        return operatorToken is "=" or "!=" or ">" or "<" or ">=" or "<=";
    }

    private static bool TryParseNumber(string value, out decimal number)
    {
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    private static void WriteHeader(TextWriter writer, IReadOnlyList<string> header)
    {
        writer.Write(CsvWriter.FormatRow(header));
        writer.Write("\r\n");
    }

    private static void WriteMatchingRows(
        TextWriter writer,
        IReadOnlyList<IReadOnlyList<string>> rows,
        int columnIndex,
        ComparisonFilter filter)
    {
        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            IReadOnlyList<string> row = rows[rowIndex];
            if (!Matches(row, columnIndex, filter))
            {
                continue;
            }

            writer.Write(CsvWriter.FormatRow(row));
            writer.Write("\r\n");
        }
    }

    private static bool Matches(IReadOnlyList<string> row, int columnIndex, ComparisonFilter filter)
    {
        string leftValue = columnIndex < row.Count ? row[columnIndex] : string.Empty;
        if (filter.LiteralIsNumeric && TryParseNumber(leftValue, out decimal leftNumericValue))
        {
            return CompareNumbers(leftNumericValue, filter.LiteralNumericValue, filter.Operator);
        }

        int comparison = string.CompareOrdinal(leftValue, filter.LiteralValue);
        return CompareText(comparison, filter.Operator);
    }

    private static bool CompareNumbers(decimal left, decimal right, ComparisonOperator comparisonOperator)
    {
        return comparisonOperator switch
        {
            ComparisonOperator.Equal => left == right,
            ComparisonOperator.NotEqual => left != right,
            ComparisonOperator.GreaterThan => left > right,
            ComparisonOperator.LessThan => left < right,
            ComparisonOperator.GreaterThanOrEqual => left >= right,
            ComparisonOperator.LessThanOrEqual => left <= right,
            _ => false
        };
    }

    private static bool CompareText(int comparison, ComparisonOperator comparisonOperator)
    {
        return comparisonOperator switch
        {
            ComparisonOperator.Equal => comparison == 0,
            ComparisonOperator.NotEqual => comparison != 0,
            ComparisonOperator.GreaterThan => comparison > 0,
            ComparisonOperator.LessThan => comparison < 0,
            ComparisonOperator.GreaterThanOrEqual => comparison >= 0,
            ComparisonOperator.LessThanOrEqual => comparison <= 0,
            _ => false
        };
    }

    private readonly record struct ComparisonFilter(
        string ColumnName,
        ComparisonOperator Operator,
        string LiteralValue,
        bool LiteralIsNumeric,
        decimal LiteralNumericValue);

    private enum ComparisonOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual
    }
}
