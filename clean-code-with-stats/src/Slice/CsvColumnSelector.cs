using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace Slice;

internal sealed class CsvTableProcessor
{
    public IReadOnlyList<string> ParseRequestedColumns(string columnsArgument)
    {
        if (string.IsNullOrWhiteSpace(columnsArgument))
        {
            return Array.Empty<string>();
        }

        return columnsArgument
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<string?> WriteSelectedColumnsAsync(
        Stream input,
        Stream output,
        IReadOnlyList<string> requestedColumns)
    {
        if (requestedColumns.Count == 0)
        {
            return "No columns were selected.";
        }

        using var parser = new TextFieldParser(input, Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };

        parser.SetDelimiters(",");

        var headers = parser.ReadFields();
        if (headers is null)
        {
            return "CSV file is empty.";
        }

        var selectedIndexes = new int[requestedColumns.Count];
        for (var i = 0; i < requestedColumns.Count; i++)
        {
            var requestedColumn = requestedColumns[i];
            var index = Array.FindIndex(headers, header => string.Equals(header, requestedColumn, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return $"Column not found: {requestedColumn}";
            }

            selectedIndexes[i] = index;
        }

        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            NewLine = "\r\n"
        };

        await writer.WriteLineAsync(BuildCsvRow(headers, selectedIndexes)).ConfigureAwait(false);

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            await writer.WriteLineAsync(BuildCsvRow(fields, selectedIndexes)).ConfigureAwait(false);
        }

        await writer.FlushAsync().ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return null;
    }

    public async Task<string?> WriteFilteredRowsAsync(
        Stream input,
        Stream output,
        string whereExpression)
    {
        if (!CsvWhereClause.TryParse(whereExpression, out var clause, out var errorMessage))
        {
            return errorMessage;
        }

        using var parser = CreateParser(input);

        var headers = parser.ReadFields();
        if (headers is null)
        {
            return "CSV file is empty.";
        }

        var filteredColumnIndex = Array.FindIndex(
            headers,
            header => string.Equals(header, clause.ColumnName, StringComparison.OrdinalIgnoreCase));

        if (filteredColumnIndex < 0)
        {
            return $"Column not found: {clause.ColumnName}";
        }

        await using var writer = CreateWriter(output);
        var allColumns = Enumerable.Range(0, headers.Length).ToArray();
        await writer.WriteLineAsync(BuildCsvRow(headers, allColumns)).ConfigureAwait(false);

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            var candidateValue = filteredColumnIndex < fields.Length ? fields[filteredColumnIndex] : string.Empty;
            if (!clause.Matches(candidateValue))
            {
                continue;
            }

            await writer.WriteLineAsync(BuildCsvRow(fields, allColumns)).ConfigureAwait(false);
        }

        await writer.FlushAsync().ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return null;
    }

    private static string BuildCsvRow(IReadOnlyList<string> fields, IReadOnlyList<int> selectedIndexes)
    {
        var selectedFields = new string[selectedIndexes.Count];
        for (var i = 0; i < selectedIndexes.Count; i++)
        {
            var selectedIndex = selectedIndexes[i];
            selectedFields[i] = selectedIndex < fields.Count ? EscapeCsvField(fields[selectedIndex]) : string.Empty;
        }

        return string.Join(",", selectedFields);
    }

    private static TextFieldParser CreateParser(Stream input)
    {
        var parser = new TextFieldParser(input, Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };

        parser.SetDelimiters(",");
        return parser;
    }

    private static StreamWriter CreateWriter(Stream output)
    {
        return new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            NewLine = "\r\n"
        };
    }

    private static string EscapeCsvField(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var requiresQuoting = value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\r') ||
            value.Contains('\n');

        if (!requiresQuoting)
        {
            return value;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private sealed record CsvWhereClause(string ColumnName, ComparisonOperator Operator, string LiteralValue)
    {
        public static bool TryParse(string whereExpression, out CsvWhereClause clause, out string errorMessage)
        {
            var trimmedExpression = whereExpression?.Trim() ?? string.Empty;
            if (trimmedExpression.Length == 0)
            {
                clause = default!;
                errorMessage = "Invalid where expression.";
                return false;
            }

            foreach (var candidate in ComparisonOperator.All)
            {
                var operatorIndex = trimmedExpression.IndexOf(candidate.Symbol, StringComparison.Ordinal);
                if (operatorIndex < 0)
                {
                    continue;
                }

                var columnName = trimmedExpression[..operatorIndex].Trim();
                var literalValue = trimmedExpression[(operatorIndex + candidate.Symbol.Length)..].Trim();
                if (columnName.Length == 0 || literalValue.Length == 0)
                {
                    break;
                }

                clause = new CsvWhereClause(columnName, candidate, literalValue);
                errorMessage = string.Empty;
                return true;
            }

            clause = default!;
            errorMessage = "Invalid where expression.";
            return false;
        }

        public bool Matches(string? candidateValue)
        {
            var leftValue = candidateValue ?? string.Empty;
            if (TryCompareNumerically(leftValue, LiteralValue, out var comparison))
            {
                return Operator.Evaluate(comparison);
            }

            comparison = string.CompareOrdinal(leftValue, LiteralValue);
            return Operator.Evaluate(comparison);
        }

        private static bool TryCompareNumerically(string leftValue, string rightValue, out int comparison)
        {
            if (decimal.TryParse(leftValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var leftNumber) &&
                decimal.TryParse(rightValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var rightNumber))
            {
                comparison = decimal.Compare(leftNumber, rightNumber);
                return true;
            }

            comparison = 0;
            return false;
        }
    }

    private sealed record ComparisonOperator(string Symbol)
    {
        public static readonly ComparisonOperator NotEqual = new("!=");
        public static readonly ComparisonOperator GreaterThanOrEqual = new(">=");
        public static readonly ComparisonOperator LessThanOrEqual = new("<=");
        public static readonly ComparisonOperator Equal = new("=");
        public static readonly ComparisonOperator GreaterThan = new(">");
        public static readonly ComparisonOperator LessThan = new("<");

        public static IReadOnlyList<ComparisonOperator> All { get; } = [
            NotEqual,
            GreaterThanOrEqual,
            LessThanOrEqual,
            Equal,
            GreaterThan,
            LessThan
        ];

        public bool Evaluate(int comparison) => Symbol switch
        {
            "=" => comparison == 0,
            "!=" => comparison != 0,
            ">" => comparison > 0,
            "<" => comparison < 0,
            ">=" => comparison >= 0,
            "<=" => comparison <= 0,
            _ => throw new InvalidOperationException($"Unsupported operator: {Symbol}")
        };
    }
}
