using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace Slice;

internal sealed class CsvTableProcessor
{
    public ExecutionOutcome LoadTable(Stream input)
    {
        using var parser = CreateParser(input);

        var headers = parser.ReadFields();
        if (headers is null)
        {
            return ExecutionOutcome.Failure("CSV file is empty.");
        }

        var rows = new List<IReadOnlyList<string>>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            rows.Add(fields);
        }

        return ExecutionOutcome.Success(new TableQueryResult(headers, rows));
    }

    public ExecutionOutcome ApplyCommand(
        QueryResult current,
        string commandName,
        IReadOnlyList<string> commandArguments)
    {
        if (current is not TableQueryResult table)
        {
            return ExecutionOutcome.Failure($"Command cannot operate on scalar result: {commandName}");
        }

        return commandName switch
        {
            "select" => SelectColumns(table, ParseRequestedColumns(commandArguments)),
            "where" => FilterRows(table, commandArguments[0]),
            "sort" => SortRows(table, commandArguments[0], commandArguments.Count > 1 ? commandArguments[1] : null),
            "head" => HeadRows(table, commandArguments[0]),
            "distinct" => DistinctRows(table, ParseRequestedColumns(commandArguments)),
            "count" => CountRows(table),
            "sum" => SumRows(table, commandArguments[0]),
            "groupby" => GroupRows(table, commandArguments),
            _ => ExecutionOutcome.Failure($"Unknown command: {commandName}")
        };
    }

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

    public ExecutionOutcome SelectColumns(
        Stream input,
        IReadOnlyList<string> requestedColumns)
    {
        var tableOutcome = LoadTable(input);
        if (tableOutcome.ErrorMessage is not null)
        {
            return tableOutcome;
        }

        return SelectColumns((TableQueryResult)tableOutcome.Result!, requestedColumns);
    }

    private ExecutionOutcome SelectColumns(
        TableQueryResult table,
        IReadOnlyList<string> requestedColumns)
    {
        if (requestedColumns.Count == 0)
        {
            return ExecutionOutcome.Failure("No columns were selected.");
        }

        if (!TryResolveRequestedColumnIndexes(table.Headers, requestedColumns, out var selectedIndexes, out var errorMessage))
        {
            return ExecutionOutcome.Failure(errorMessage!);
        }

        var selectedHeaders = selectedIndexes.Select(index => table.Headers[index]).ToArray();
        var rows = new List<IReadOnlyList<string>>();
        foreach (var row in table.Rows)
        {
            rows.Add(BuildSelectedRow(row, selectedIndexes));
        }

        return ExecutionOutcome.Success(new TableQueryResult(selectedHeaders, rows));
    }

    public ExecutionOutcome DistinctRows(
        Stream input,
        IReadOnlyList<string> requestedColumns)
    {
        var tableOutcome = LoadTable(input);
        if (tableOutcome.ErrorMessage is not null)
        {
            return tableOutcome;
        }

        return DistinctRows((TableQueryResult)tableOutcome.Result!, requestedColumns);
    }

    private ExecutionOutcome DistinctRows(
        TableQueryResult table,
        IReadOnlyList<string> requestedColumns)
    {
        if (requestedColumns.Count == 0)
        {
            return ExecutionOutcome.Failure("No columns were selected.");
        }

        if (!TryResolveRequestedColumnIndexes(table.Headers, requestedColumns, out var selectedIndexes, out var errorMessage))
        {
            return ExecutionOutcome.Failure(errorMessage!);
        }

        var seenRows = new HashSet<string[]>(new StringArraySequenceComparer());
        var rows = new List<IReadOnlyList<string>>();
        foreach (var row in table.Rows)
        {
            var distinctKey = BuildDistinctKey(row, selectedIndexes);
            if (!seenRows.Add(distinctKey))
            {
                continue;
            }

            rows.Add(BuildSelectedRow(row, selectedIndexes));
        }

        var selectedHeaders = selectedIndexes.Select(index => table.Headers[index]).ToArray();
        return ExecutionOutcome.Success(new TableQueryResult(selectedHeaders, rows));
    }

    public ExecutionOutcome FilterRows(
        Stream input,
        string whereExpression)
    {
        var tableOutcome = LoadTable(input);
        if (tableOutcome.ErrorMessage is not null)
        {
            return tableOutcome;
        }

        return FilterRows((TableQueryResult)tableOutcome.Result!, whereExpression);
    }

    private ExecutionOutcome FilterRows(
        TableQueryResult table,
        string whereExpression)
    {
        if (!CsvWhereClause.TryParse(whereExpression, out var clause, out var errorMessage))
        {
            return ExecutionOutcome.Failure(errorMessage);
        }

        var filteredColumnIndex = Array.FindIndex(
            table.Headers.ToArray(),
            header => string.Equals(header, clause.ColumnName, StringComparison.OrdinalIgnoreCase));

        if (filteredColumnIndex < 0)
        {
            return ExecutionOutcome.Failure($"Column not found: {clause.ColumnName}");
        }

        var rows = new List<IReadOnlyList<string>>();
        foreach (var fields in table.Rows)
        {
            var candidateValue = filteredColumnIndex < fields.Count ? fields[filteredColumnIndex] : string.Empty;
            if (!clause.Matches(candidateValue))
            {
                continue;
            }

            rows.Add(fields);
        }

        return ExecutionOutcome.Success(new TableQueryResult(table.Headers, rows));
    }

    public ExecutionOutcome SortRows(
        Stream input,
        string columnName,
        string? directionArgument)
    {
        var tableOutcome = LoadTable(input);
        if (tableOutcome.ErrorMessage is not null)
        {
            return tableOutcome;
        }

        return SortRows((TableQueryResult)tableOutcome.Result!, columnName, directionArgument);
    }

    private ExecutionOutcome SortRows(
        TableQueryResult table,
        string columnName,
        string? directionArgument)
    {
        if (!TryParseSortDirection(directionArgument, out var sortDirection, out var errorMessage))
        {
            return ExecutionOutcome.Failure(errorMessage!);
        }

        var sortColumnIndex = Array.FindIndex(
            table.Headers.ToArray(),
            header => string.Equals(header, columnName, StringComparison.OrdinalIgnoreCase));

        if (sortColumnIndex < 0)
        {
            return ExecutionOutcome.Failure($"Column not found: {columnName}");
        }

        var rows = new List<CsvRow>();
        foreach (var fields in table.Rows)
        {
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

        var sortedRows = orderedRows.Select(row => row.Fields).ToArray();
        return ExecutionOutcome.Success(new TableQueryResult(table.Headers, sortedRows));
    }

    public ExecutionOutcome HeadRows(
        Stream input,
        string rowCountArgument)
    {
        var tableOutcome = LoadTable(input);
        if (tableOutcome.ErrorMessage is not null)
        {
            return tableOutcome;
        }

        return HeadRows((TableQueryResult)tableOutcome.Result!, rowCountArgument);
    }

    private ExecutionOutcome HeadRows(
        TableQueryResult table,
        string rowCountArgument)
    {
        if (!TryParsePositiveRowCount(rowCountArgument, out var rowCount, out var errorMessage))
        {
            return ExecutionOutcome.Failure(errorMessage!);
        }

        var rows = new List<IReadOnlyList<string>>();
        foreach (var row in table.Rows)
        {
            if (rowCount == 0)
            {
                break;
            }

            rows.Add(row);
            rowCount--;
        }

        return ExecutionOutcome.Success(new TableQueryResult(table.Headers, rows));
    }

    public ExecutionOutcome CountRows(Stream input)
    {
        var tableOutcome = LoadTable(input);
        if (tableOutcome.ErrorMessage is not null)
        {
            return tableOutcome;
        }

        return CountRows((TableQueryResult)tableOutcome.Result!);
    }

    private ExecutionOutcome CountRows(TableQueryResult table)
    {
        return ExecutionOutcome.Success(new ScalarQueryResult(table.Rows.Count));
    }

    public ExecutionOutcome SumRows(Stream input, string columnName)
    {
        var tableOutcome = LoadTable(input);
        if (tableOutcome.ErrorMessage is not null)
        {
            return tableOutcome;
        }

        return SumRows((TableQueryResult)tableOutcome.Result!, columnName);
    }

    private ExecutionOutcome SumRows(TableQueryResult table, string columnName)
    {
        var columnIndex = Array.FindIndex(
            table.Headers.ToArray(),
            header => string.Equals(header, columnName, StringComparison.OrdinalIgnoreCase));

        if (columnIndex < 0)
        {
            return ExecutionOutcome.Failure($"Column not found: {columnName}");
        }

        decimal total = 0m;
        foreach (var fields in table.Rows)
        {
            var candidateValue = columnIndex < fields.Count ? fields[columnIndex] : string.Empty;
            if (!decimal.TryParse(candidateValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericValue))
            {
                return ExecutionOutcome.Failure($"Column must contain only numeric values: {columnName}");
            }

            total += numericValue;
        }

        return ExecutionOutcome.Success(new ScalarQueryResult(total));
    }

    public ExecutionOutcome GroupRows(
        Stream input,
        IReadOnlyList<string> groupByArguments)
    {
        var tableOutcome = LoadTable(input);
        if (tableOutcome.ErrorMessage is not null)
        {
            return tableOutcome;
        }

        return GroupRows((TableQueryResult)tableOutcome.Result!, groupByArguments);
    }

    private ExecutionOutcome GroupRows(
        TableQueryResult table,
        IReadOnlyList<string> groupByArguments)
    {
        if (!GroupByRequest.TryParse(groupByArguments, out var request, out var errorMessage))
        {
            return ExecutionOutcome.Failure(errorMessage);
        }

        if (!TryResolveColumnIndex(table.Headers, request.GroupColumnName, out var groupColumnIndex))
        {
            return ExecutionOutcome.Failure($"Column not found: {request.GroupColumnName}");
        }

        var aggregateColumnIndex = -1;
        if (request.AggregateKind is GroupByAggregateKind.Sum)
        {
            if (!TryResolveColumnIndex(table.Headers, request.AggregateColumnName!, out aggregateColumnIndex))
            {
                return ExecutionOutcome.Failure($"Column not found: {request.AggregateColumnName}");
            }
        }

        var groups = new List<GroupAggregation>();
        var groupLookup = new Dictionary<string, GroupAggregation>(StringComparer.Ordinal);

        foreach (var fields in table.Rows)
        {
            var groupValue = groupColumnIndex < fields.Count ? fields[groupColumnIndex] : string.Empty;
            if (!groupLookup.TryGetValue(groupValue, out var aggregation))
            {
                aggregation = new GroupAggregation(groupValue);
                groupLookup.Add(groupValue, aggregation);
                groups.Add(aggregation);
            }

            aggregation.RowCount++;

            if (request.AggregateKind is not GroupByAggregateKind.Sum)
            {
                continue;
            }

            var candidateValue = aggregateColumnIndex < fields.Count ? fields[aggregateColumnIndex] : string.Empty;
            if (!decimal.TryParse(candidateValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericValue))
            {
                return ExecutionOutcome.Failure($"Column must contain only numeric values: {request.AggregateColumnName}");
            }

            aggregation.Sum += numericValue;
        }

        var rows = new List<IReadOnlyList<string>>(groups.Count);
        foreach (var group in groups)
        {
            var aggregateValue = request.AggregateKind is GroupByAggregateKind.Count
                ? group.RowCount.ToString(CultureInfo.InvariantCulture)
                : FormatDecimal(group.Sum);

            rows.Add([group.GroupValue, aggregateValue]);
        }

        var resultHeaders = request.AggregateKind is GroupByAggregateKind.Count
            ? new[] { request.GroupColumnName, "count" }
            : new[] { request.GroupColumnName, request.AggregateColumnName! };

        return ExecutionOutcome.Success(new TableQueryResult(resultHeaders, rows));
    }

    private static string[] BuildSelectedRow(IReadOnlyList<string> fields, IReadOnlyList<int> selectedIndexes)
    {
        var selectedFields = new string[selectedIndexes.Count];
        for (var i = 0; i < selectedIndexes.Count; i++)
        {
            var selectedIndex = selectedIndexes[i];
            selectedFields[i] = selectedIndex < fields.Count ? fields[selectedIndex] : string.Empty;
        }

        return selectedFields;
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

    private static bool TryResolveColumnIndex(
        IReadOnlyList<string> headers,
        string requestedColumn,
        out int index)
    {
        for (var headerIndex = 0; headerIndex < headers.Count; headerIndex++)
        {
            if (string.Equals(headers[headerIndex], requestedColumn, StringComparison.OrdinalIgnoreCase))
            {
                index = headerIndex;
                return true;
            }
        }

        index = -1;
        return false;
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

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("0.#############################", CultureInfo.InvariantCulture);
    }

    private sealed record CsvRow(IReadOnlyList<string> Fields, SortValue SortValue);

    private sealed record SortValue(bool IsNumeric, decimal NumericValue, string TextValue);

    private sealed record GroupAggregation(string GroupValue)
    {
        public int RowCount { get; set; }

        public decimal Sum { get; set; }
    }

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

    private enum GroupByAggregateKind
    {
        Count,
        Sum
    }

    private sealed record GroupByRequest(string GroupColumnName, GroupByAggregateKind AggregateKind, string? AggregateColumnName)
    {
        public static bool TryParse(
            IReadOnlyList<string> arguments,
            out GroupByRequest request,
            out string errorMessage)
        {
            if (arguments.Count is not 2 and not 3)
            {
                request = default!;
                errorMessage = "Invalid groupby arguments.";
                return false;
            }

            var groupColumnName = arguments[0];
            var aggregateName = arguments[1];

            if (string.Equals(aggregateName, "count", StringComparison.OrdinalIgnoreCase))
            {
                if (arguments.Count != 2)
                {
                    request = default!;
                    errorMessage = "Invalid groupby arguments.";
                    return false;
                }

                request = new GroupByRequest(groupColumnName, GroupByAggregateKind.Count, null);
                errorMessage = string.Empty;
                return true;
            }

            if (string.Equals(aggregateName, "sum", StringComparison.OrdinalIgnoreCase))
            {
                if (arguments.Count != 3)
                {
                    request = default!;
                    errorMessage = "Invalid groupby arguments.";
                    return false;
                }

                request = new GroupByRequest(groupColumnName, GroupByAggregateKind.Sum, arguments[2]);
                errorMessage = string.Empty;
                return true;
            }

            request = default!;
            errorMessage = $"Invalid groupby aggregate: {aggregateName}";
            return false;
        }
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
