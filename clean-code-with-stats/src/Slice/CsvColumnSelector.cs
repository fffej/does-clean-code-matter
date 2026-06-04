using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace Slice;

internal sealed class CsvTableProcessor
{
    public IReadOnlyList<string> ParseRequestedColumns(string columnsArgument)
    {
        return ParseRequestedColumns(new[] { columnsArgument });
    }

    public IReadOnlyList<string> ParseRequestedColumns(IEnumerable<string> columnArguments)
    {
        var requestedColumns = new List<string>();

        foreach (var columnArgument in columnArguments)
        {
            if (string.IsNullOrWhiteSpace(columnArgument))
            {
                continue;
            }

            requestedColumns.AddRange(
                columnArgument.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return requestedColumns;
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

        if (!TryResolveRequestedColumnIndexes(headers, requestedColumns, out var selectedIndexes, out var errorMessage))
        {
            return errorMessage;
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

    public async Task<string?> WriteDistinctRowsAsync(
        Stream input,
        Stream output,
        IReadOnlyList<string> requestedColumns)
    {
        if (requestedColumns.Count == 0)
        {
            return "No columns were selected.";
        }

        using var parser = CreateParser(input);

        var headers = parser.ReadFields();
        if (headers is null)
        {
            return "CSV file is empty.";
        }

        if (!TryResolveRequestedColumnIndexes(headers, requestedColumns, out var selectedIndexes, out var errorMessage))
        {
            return errorMessage;
        }

        var seenRows = new HashSet<string[]>(new StringArraySequenceComparer());

        await using var writer = CreateWriter(output);
        await writer.WriteLineAsync(BuildCsvRow(headers, selectedIndexes)).ConfigureAwait(false);

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            var distinctKey = BuildDistinctKey(fields, selectedIndexes);
            if (!seenRows.Add(distinctKey))
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

    public async Task<string?> WriteSortedRowsAsync(
        Stream input,
        Stream output,
        string columnName,
        string? directionArgument)
    {
        if (!TryParseSortDirection(directionArgument, out var sortDirection, out var errorMessage))
        {
            return errorMessage;
        }

        using var parser = CreateParser(input);

        var headers = parser.ReadFields();
        if (headers is null)
        {
            return "CSV file is empty.";
        }

        var sortColumnIndex = Array.FindIndex(
            headers,
            header => string.Equals(header, columnName, StringComparison.OrdinalIgnoreCase));

        if (sortColumnIndex < 0)
        {
            return $"Column not found: {columnName}";
        }

        var rows = new List<CsvRow>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            rows.Add(new CsvRow(fields, GetSortValue(fields, sortColumnIndex)));
        }

        var allRowsAreNumeric = rows.All(row => row.SortValue.IsNumeric);
        var orderedRows = allRowsAreNumeric
            ? sortDirection == SortDirection.Ascending
                ? rows.OrderBy(row => row.SortValue.NumericValue)
                : rows.OrderByDescending(row => row.SortValue.NumericValue)
            : sortDirection == SortDirection.Ascending
                ? rows.OrderBy(row => row.SortValue.TextValue, StringComparer.Ordinal)
                : rows.OrderByDescending(row => row.SortValue.TextValue, StringComparer.Ordinal);

        await using var writer = CreateWriter(output);
        await writer.WriteLineAsync(BuildCsvRow(headers)).ConfigureAwait(false);

        foreach (var row in orderedRows)
        {
            await writer.WriteLineAsync(BuildCsvRow(row.Fields)).ConfigureAwait(false);
        }

        await writer.FlushAsync().ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return null;
    }

    public async Task<string?> WriteHeadRowsAsync(
        Stream input,
        Stream output,
        string rowCountArgument)
    {
        if (!TryParsePositiveRowCount(rowCountArgument, out var rowCount, out var errorMessage))
        {
            return errorMessage;
        }

        using var parser = CreateParser(input);

        var headers = parser.ReadFields();
        if (headers is null)
        {
            return "CSV file is empty.";
        }

        await using var writer = CreateWriter(output);
        await writer.WriteLineAsync(BuildCsvRow(headers)).ConfigureAwait(false);

        while (rowCount > 0 && !parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            await writer.WriteLineAsync(BuildCsvRow(fields)).ConfigureAwait(false);
            rowCount--;
        }

        await writer.FlushAsync().ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return null;
    }

    public async Task<string?> WriteCountAsync(Stream input, Stream output)
    {
        using var parser = CreateParser(input);

        var headers = parser.ReadFields();
        if (headers is null)
        {
            return "CSV file is empty.";
        }

        var rowCount = 0;
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            rowCount++;
        }

        await using var writer = CreateWriter(output);
        await writer.WriteLineAsync(rowCount.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return null;
    }

    public async Task<string?> WriteSumAsync(Stream input, Stream output, string columnName)
    {
        using var parser = CreateParser(input);

        var headers = parser.ReadFields();
        if (headers is null)
        {
            return "CSV file is empty.";
        }

        var columnIndex = Array.FindIndex(
            headers,
            header => string.Equals(header, columnName, StringComparison.OrdinalIgnoreCase));

        if (columnIndex < 0)
        {
            return $"Column not found: {columnName}";
        }

        decimal total = 0m;
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            var candidateValue = columnIndex < fields.Length ? fields[columnIndex] : string.Empty;
            if (!decimal.TryParse(candidateValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericValue))
            {
                return $"Column must contain only numeric values: {columnName}";
            }

            total += numericValue;
        }

        await using var writer = CreateWriter(output);
        await writer.WriteLineAsync(total.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
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

    private static string BuildCsvRow(IReadOnlyList<string> fields)
    {
        var outputFields = new string[fields.Count];
        for (var i = 0; i < fields.Count; i++)
        {
            outputFields[i] = EscapeCsvField(fields[i]);
        }

        return string.Join(",", outputFields);
    }

    private static bool TryResolveRequestedColumnIndexes(
        IReadOnlyList<string> headers,
        IReadOnlyList<string> requestedColumns,
        out int[] selectedIndexes,
        out string? errorMessage)
    {
        selectedIndexes = new int[requestedColumns.Count];

        for (var i = 0; i < requestedColumns.Count; i++)
        {
            var requestedColumn = requestedColumns[i];
            var index = -1;
            for (var headerIndex = 0; headerIndex < headers.Count; headerIndex++)
            {
                if (string.Equals(headers[headerIndex], requestedColumn, StringComparison.OrdinalIgnoreCase))
                {
                    index = headerIndex;
                    break;
                }
            }

            if (index < 0)
            {
                errorMessage = $"Column not found: {requestedColumn}";
                selectedIndexes = Array.Empty<int>();
                return false;
            }

            selectedIndexes[i] = index;
        }

        errorMessage = null;
        return true;
    }

    private static string[] BuildDistinctKey(IReadOnlyList<string> fields, IReadOnlyList<int> selectedIndexes)
    {
        var selectedFields = new string[selectedIndexes.Count];
        for (var i = 0; i < selectedIndexes.Count; i++)
        {
            var selectedIndex = selectedIndexes[i];
            selectedFields[i] = selectedIndex < fields.Count ? fields[selectedIndex] : string.Empty;
        }

        return selectedFields;
    }

    private static SortValue GetSortValue(IReadOnlyList<string> fields, int sortColumnIndex)
    {
        var value = sortColumnIndex < fields.Count ? fields[sortColumnIndex] : string.Empty;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericValue))
        {
            return new SortValue(true, numericValue, value);
        }

        return new SortValue(false, default, value);
    }

    private static bool TryParseSortDirection(
        string? directionArgument,
        out SortDirection sortDirection,
        out string? errorMessage)
    {
        if (directionArgument is null)
        {
            sortDirection = SortDirection.Ascending;
            errorMessage = null;
            return true;
        }

        if (string.Equals(directionArgument, "asc", StringComparison.OrdinalIgnoreCase))
        {
            sortDirection = SortDirection.Ascending;
            errorMessage = null;
            return true;
        }

        if (string.Equals(directionArgument, "desc", StringComparison.OrdinalIgnoreCase))
        {
            sortDirection = SortDirection.Descending;
            errorMessage = null;
            return true;
        }

        sortDirection = SortDirection.Ascending;
        errorMessage = $"Invalid sort direction: {directionArgument}";
        return false;
    }

    private static bool TryParsePositiveRowCount(
        string rowCountArgument,
        out int rowCount,
        out string? errorMessage)
    {
        if (int.TryParse(rowCountArgument, NumberStyles.Integer, CultureInfo.InvariantCulture, out rowCount) && rowCount > 0)
        {
            errorMessage = null;
            return true;
        }

        rowCount = default;
        errorMessage = $"Invalid row count: {rowCountArgument}";
        return false;
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

    private sealed record CsvRow(IReadOnlyList<string> Fields, SortValue SortValue);

    private sealed record SortValue(bool IsNumeric, decimal NumericValue, string TextValue);

    private sealed class StringArraySequenceComparer : IEqualityComparer<string[]>
    {
        public bool Equals(string[]? x, string[]? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null || x.Length != y.Length)
            {
                return false;
            }

            for (var i = 0; i < x.Length; i++)
            {
                if (!string.Equals(x[i], y[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(string[] obj)
        {
            var hashCode = new HashCode();
            foreach (var value in obj)
            {
                hashCode.Add(value, StringComparer.Ordinal);
            }

            return hashCode.ToHashCode();
        }
    }

    private enum SortDirection
    {
        Ascending,
        Descending
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
