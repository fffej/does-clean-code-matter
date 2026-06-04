using System.Globalization;
using System.Text;

namespace Slice;

internal static class CsvQueryOperations
{
    public static bool TryReadInitialTable(string csvPath, TextWriter error, out QueryResult.Table table)
    {
        table = default!;

        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return false;
        }

        table = new QueryResult.Table(rows[0], rows[1..]);
        return true;
    }

    public static bool TryApply(
        QueryResult current,
        string commandName,
        IReadOnlyList<string> arguments,
        TextWriter error,
        out QueryResult next)
    {
        if (current is not QueryResult.Table table)
        {
            error.WriteLine($"Command '{commandName}' cannot be applied to an aggregate result.");
            next = default!;
            return false;
        }

        switch (commandName)
        {
            case "where" when arguments.Count == 1:
                if (TryApplyWhere(table, arguments[0], error, out QueryResult.Table whereResult))
                {
                    next = whereResult;
                    return true;
                }

                next = default!;
                return false;
            case "sort" when arguments.Count == 1:
                if (TryApplySort(table, arguments[0], string.Empty, error, out QueryResult.Table sortResult))
                {
                    next = sortResult;
                    return true;
                }

                next = default!;
                return false;
            case "sort" when arguments.Count == 2:
                if (TryApplySort(table, arguments[0], arguments[1], error, out QueryResult.Table sortedResult))
                {
                    next = sortedResult;
                    return true;
                }

                next = default!;
                return false;
            case "head" when arguments.Count == 1:
                if (TryApplyHead(table, arguments[0], error, out QueryResult.Table headResult))
                {
                    next = headResult;
                    return true;
                }

                next = default!;
                return false;
            case "select" when arguments.Count == 1:
                if (TryApplySelect(table, arguments[0], error, out QueryResult.Table selectResult))
                {
                    next = selectResult;
                    return true;
                }

                next = default!;
                return false;
            case "distinct" when arguments.Count >= 1:
                if (TryApplyDistinct(table, arguments.ToArray(), error, out QueryResult.Table distinctResult))
                {
                    next = distinctResult;
                    return true;
                }

                next = default!;
                return false;
            case "groupby" when arguments.Count >= 2:
                if (TryApplyGroupBy(table, arguments[0], arguments.Skip(1).ToArray(), error, out QueryResult.Table groupByResult))
                {
                    next = groupByResult;
                    return true;
                }

                next = default!;
                return false;
            case "count" when arguments.Count == 0:
                return TryApplyCount(table, arguments, error, out next);
            case "sum" when arguments.Count == 1:
                return TryApplySum(table, arguments, error, out next);
            case "where":
                error.WriteLine("Invalid where expression.");
                next = default!;
                return false;
            case "sort":
                error.WriteLine($"Invalid sort expression: {string.Join(" ", arguments)}".TrimEnd());
                next = default!;
                return false;
            case "head":
                error.WriteLine($"Invalid head count: {string.Join(" ", arguments)}");
                next = default!;
                return false;
            case "select":
                error.WriteLine("No columns were specified.");
                next = default!;
                return false;
            case "distinct":
                error.WriteLine("No columns were specified.");
                next = default!;
                return false;
            case "groupby":
                error.WriteLine("Invalid groupby expression.");
                next = default!;
                return false;
            case "count":
                error.WriteLine("Invalid count expression.");
                next = default!;
                return false;
            case "sum":
                error.WriteLine("Invalid sum expression.");
                next = default!;
                return false;
            default:
                return TryFailUnknownCommand(commandName, error, out next);
        }
    }

    public static bool TryApplyWhere(
        QueryResult.Table table,
        string filterExpression,
        TextWriter error,
        out QueryResult.Table result)
    {
        if (!TryParseFilter(filterExpression, error, out ComparisonFilter filter))
        {
            result = default!;
            return false;
        }

        int columnIndex = CsvHeaderLookup.FindHeaderIndex(table.Header, filter.ColumnName);
        if (columnIndex < 0)
        {
            error.WriteLine($"Column not found: {filter.ColumnName}");
            result = default!;
            return false;
        }

        result = new QueryResult.Table(table.Header, GetMatchingRows(table.Rows, columnIndex, filter));
        return true;
    }

    public static bool TryApplySort(
        QueryResult.Table table,
        string sortColumn,
        string sortDirection,
        TextWriter error,
        out QueryResult.Table result)
    {
        if (!TryParseSortArgument(sortColumn, sortDirection, out SortSpecification specification))
        {
            error.WriteLine($"Invalid sort expression: {sortColumn} {sortDirection}".TrimEnd());
            result = default!;
            return false;
        }

        int columnIndex = CsvHeaderLookup.FindHeaderIndex(table.Header, specification.ColumnName);
        if (columnIndex < 0)
        {
            error.WriteLine($"Column not found: {specification.ColumnName}");
            result = default!;
            return false;
        }

        bool sortAsNumbers = CanSortAsNumbers(table.Rows, columnIndex);
        IEnumerable<IReadOnlyList<string>> sortedRows = sortAsNumbers
            ? SortRowsNumerically(table.Rows, columnIndex, specification.Direction)
            : SortRowsAsText(table.Rows, columnIndex, specification.Direction);

        result = new QueryResult.Table(table.Header, sortedRows.ToList());
        return true;
    }

    public static bool TryApplyHead(
        QueryResult.Table table,
        string rowCountArgument,
        TextWriter error,
        out QueryResult.Table result)
    {
        if (!TryParseRowCount(rowCountArgument, out int rowCount))
        {
            error.WriteLine($"Invalid head count: {rowCountArgument}");
            result = default!;
            return false;
        }

        return TryApplyHead(table, rowCount, out result);
    }

    public static bool TryApplyHead(
        QueryResult.Table table,
        int rowCount,
        out QueryResult.Table result)
    {
        if (rowCount <= 0)
        {
            result = default!;
            return false;
        }

        result = new QueryResult.Table(table.Header, GetRows(table.Rows, rowCount));
        return true;
    }

    public static bool TryApplySelect(
        QueryResult.Table table,
        string selectedColumnsArgument,
        TextWriter error,
        out QueryResult.Table result)
    {
        IReadOnlyList<string> selectedColumns = ParseSelectedColumns(selectedColumnsArgument, error);
        if (selectedColumns.Count == 0)
        {
            result = default!;
            return false;
        }

        int[] columnIndexes = ResolveColumnIndexes(table.Header, selectedColumns, error);
        if (columnIndexes.Length == 0)
        {
            result = default!;
            return false;
        }

        result = new QueryResult.Table(GetSelectedValues(table.Header, columnIndexes), GetSelectedRows(table.Rows, columnIndexes));
        return true;
    }

    public static bool TryApplyDistinct(
        QueryResult.Table table,
        string[] distinctColumnsArguments,
        TextWriter error,
        out QueryResult.Table result)
    {
        IReadOnlyList<string> distinctColumns = ParseDistinctColumns(distinctColumnsArguments, error);
        if (distinctColumns.Count == 0)
        {
            result = default!;
            return false;
        }

        int[] columnIndexes = ResolveColumnIndexes(table.Header, distinctColumns, error);
        if (columnIndexes.Length == 0)
        {
            result = default!;
            return false;
        }

        result = new QueryResult.Table(GetSelectedValues(table.Header, columnIndexes), GetDistinctRows(table.Rows, columnIndexes));
        return true;
    }

    public static bool TryApplyGroupBy(
        QueryResult.Table table,
        string groupColumn,
        string[] aggregateArguments,
        TextWriter error,
        out QueryResult.Table result)
    {
        if (!TryParseAggregate(aggregateArguments, error, out AggregateSpecification specification))
        {
            result = default!;
            return false;
        }

        int groupColumnIndex = CsvHeaderLookup.FindHeaderIndex(table.Header, groupColumn);
        if (groupColumnIndex < 0)
        {
            error.WriteLine($"Column not found: {groupColumn}");
            result = default!;
            return false;
        }

        int aggregateColumnIndex = -1;
        if (specification.Kind == AggregateKind.Sum)
        {
            aggregateColumnIndex = CsvHeaderLookup.FindHeaderIndex(table.Header, specification.ColumnName!);
            if (aggregateColumnIndex < 0)
            {
                error.WriteLine($"Column not found: {specification.ColumnName}");
                result = default!;
                return false;
            }
        }

        Dictionary<string, GroupAggregateState> aggregatesByGroup = new(StringComparer.Ordinal);
        List<string> groupOrder = [];

        foreach (IReadOnlyList<string> row in table.Rows)
        {
            string groupValue = GetColumnValue(row, groupColumnIndex);

            if (!aggregatesByGroup.TryGetValue(groupValue, out GroupAggregateState? state))
            {
                state = new GroupAggregateState();
                aggregatesByGroup.Add(groupValue, state);
                groupOrder.Add(groupValue);
            }

            if (specification.Kind == AggregateKind.Count)
            {
                state.Count++;
                continue;
            }

            string value = GetColumnValue(row, aggregateColumnIndex);
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsedValue))
            {
                error.WriteLine($"Non-numeric value found in column {specification.ColumnName}: {value}");
                result = default!;
                return false;
            }

            state.Sum += parsedValue;
        }

        List<IReadOnlyList<string>> resultRows = [];
        foreach (string groupValue in groupOrder)
        {
            GroupAggregateState state = aggregatesByGroup[groupValue];
            string aggregateValue = specification.Kind == AggregateKind.Count
                ? state.Count.ToString(CultureInfo.InvariantCulture)
                : state.Sum.ToString("G29", CultureInfo.InvariantCulture);

            resultRows.Add([groupValue, aggregateValue]);
        }

        string aggregateHeader = specification.Kind == AggregateKind.Count
            ? "count"
            : $"sum_{specification.ColumnName}";

        result = new QueryResult.Table([groupColumn, aggregateHeader], resultRows);
        return true;
    }

    public static bool TryApplyCount(
        QueryResult.Table table,
        IReadOnlyList<string> arguments,
        TextWriter error,
        out QueryResult result)
    {
        if (arguments.Count != 0)
        {
            error.WriteLine("Invalid count expression.");
            result = default!;
            return false;
        }

        result = new QueryResult.Scalar(table.Rows.Count.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    public static bool TryApplySum(
        QueryResult.Table table,
        IReadOnlyList<string> arguments,
        TextWriter error,
        out QueryResult result)
    {
        if (arguments.Count != 1)
        {
            error.WriteLine("Invalid sum expression.");
            result = default!;
            return false;
        }

        string columnName = arguments[0];
        int columnIndex = CsvHeaderLookup.FindHeaderIndex(table.Header, columnName);
        if (columnIndex < 0)
        {
            error.WriteLine($"Column not found: {columnName}");
            result = default!;
            return false;
        }

        if (!TrySumColumn(table.Rows, columnIndex, out decimal sum, out string? invalidValue))
        {
            error.WriteLine($"Non-numeric value found in column {columnName}: {invalidValue}");
            result = default!;
            return false;
        }

        result = new QueryResult.Scalar(sum.ToString("G29", CultureInfo.InvariantCulture));
        return true;
    }

    private static bool TryFailUnknownCommand(string commandName, TextWriter error, out QueryResult result)
    {
        error.WriteLine($"Unknown command: {commandName}");
        result = default!;
        return false;
    }

    private static bool TryParseRowCount(string rowCountArgument, out int rowCount)
    {
        return int.TryParse(rowCountArgument, out rowCount) && rowCount > 0;
    }

    private static IReadOnlyList<IReadOnlyList<string>> GetRows(IReadOnlyList<IReadOnlyList<string>> rows, int rowCount)
    {
        int rowsToWrite = Math.Min(rows.Count, rowCount);
        List<IReadOnlyList<string>> selectedRows = [];
        for (int rowIndex = 0; rowIndex < rowsToWrite; rowIndex++)
        {
            selectedRows.Add(rows[rowIndex]);
        }

        return selectedRows;
    }

    private static IReadOnlyList<string> ParseSelectedColumns(string selectedColumnsArgument, TextWriter error)
    {
        string[] selectedColumns = selectedColumnsArgument.Split(',', StringSplitOptions.TrimEntries);
        if (selectedColumns.Length == 0 || selectedColumns.Any(string.IsNullOrWhiteSpace))
        {
            error.WriteLine("No columns were specified.");
            return Array.Empty<string>();
        }

        return selectedColumns;
    }

    private static int[] ResolveColumnIndexes(
        IReadOnlyList<string> header,
        IReadOnlyList<string> selectedColumns,
        TextWriter error)
    {
        int[] indexes = new int[selectedColumns.Count];
        for (int columnIndex = 0; columnIndex < selectedColumns.Count; columnIndex++)
        {
            string selectedColumn = selectedColumns[columnIndex];
            int headerIndex = CsvHeaderLookup.FindHeaderIndex(header, selectedColumn);
            if (headerIndex < 0)
            {
                error.WriteLine($"Column not found: {selectedColumn}");
                return Array.Empty<int>();
            }

            indexes[columnIndex] = headerIndex;
        }

        return indexes;
    }

    private static IReadOnlyList<IReadOnlyList<string>> GetSelectedRows(
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<int> columnIndexes)
    {
        List<IReadOnlyList<string>> selectedRows = [];
        foreach (IReadOnlyList<string> row in rows)
        {
            selectedRows.Add(GetSelectedValues(row, columnIndexes));
        }

        return selectedRows;
    }

    private static string[] GetSelectedValues(IReadOnlyList<string> row, IReadOnlyList<int> columnIndexes)
    {
        string[] selectedValues = new string[columnIndexes.Count];
        for (int columnIndex = 0; columnIndex < columnIndexes.Count; columnIndex++)
        {
            int sourceIndex = columnIndexes[columnIndex];
            selectedValues[columnIndex] = sourceIndex < row.Count ? row[sourceIndex] : string.Empty;
        }

        return selectedValues;
    }

    private static IReadOnlyList<string> ParseDistinctColumns(string[] distinctColumnsArguments, TextWriter error)
    {
        List<string> distinctColumns = [];

        foreach (string argument in distinctColumnsArguments)
        {
            string[] tokens = argument.Split(',', StringSplitOptions.TrimEntries);
            foreach (string token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    error.WriteLine("No columns were specified.");
                    return Array.Empty<string>();
                }

                distinctColumns.Add(token);
            }
        }

        if (distinctColumns.Count == 0)
        {
            error.WriteLine("No columns were specified.");
            return Array.Empty<string>();
        }

        return distinctColumns;
    }

    private static IReadOnlyList<IReadOnlyList<string>> GetDistinctRows(
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<int> columnIndexes)
    {
        List<IReadOnlyList<string>> distinctRows = [];
        HashSet<string> seenKeys = [];

        foreach (IReadOnlyList<string> row in rows)
        {
            string distinctKey = BuildDistinctKey(row, columnIndexes);
            if (!seenKeys.Add(distinctKey))
            {
                continue;
            }

            distinctRows.Add(GetSelectedValues(row, columnIndexes));
        }

        return distinctRows;
    }

    private static string BuildDistinctKey(IReadOnlyList<string> row, IReadOnlyList<int> columnIndexes)
    {
        var builder = new StringBuilder();
        for (int columnIndex = 0; columnIndex < columnIndexes.Count; columnIndex++)
        {
            if (columnIndex > 0)
            {
                builder.Append('\u001F');
            }

            string value = GetColumnValue(row, columnIndexes[columnIndex]);
            builder.Append(value.Length);
            builder.Append(':');
            builder.Append(value);
        }

        return builder.ToString();
    }

    private static string GetColumnValue(IReadOnlyList<string> row, int columnIndex)
    {
        return columnIndex < row.Count ? row[columnIndex] : string.Empty;
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

    private static IReadOnlyList<IReadOnlyList<string>> GetMatchingRows(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int columnIndex,
        ComparisonFilter filter)
    {
        List<IReadOnlyList<string>> matchingRows = [];
        foreach (IReadOnlyList<string> row in rows)
        {
            if (!Matches(row, columnIndex, filter))
            {
                continue;
            }

            matchingRows.Add(row);
        }

        return matchingRows;
    }

    private static bool Matches(IReadOnlyList<string> row, int columnIndex, ComparisonFilter filter)
    {
        string leftValue = GetColumnValue(row, columnIndex);
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

    private static bool TryParseSortArgument(string sortColumn, string sortDirection, out SortSpecification specification)
    {
        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            specification = default;
            return false;
        }

        SortDirection direction = SortDirection.Ascending;
        if (!string.IsNullOrWhiteSpace(sortDirection))
        {
            string normalizedDirection = sortDirection.Trim().ToLowerInvariant();
            direction = normalizedDirection switch
            {
                "asc" => SortDirection.Ascending,
                "desc" => SortDirection.Descending,
                _ => default
            };

            if (normalizedDirection is not "asc" and not "desc")
            {
                specification = default;
                return false;
            }
        }

        specification = new SortSpecification(sortColumn.Trim(), direction);
        return true;
    }

    private static bool CanSortAsNumbers(IReadOnlyList<IReadOnlyList<string>> rows, int columnIndex)
    {
        foreach (IReadOnlyList<string> row in rows)
        {
            string value = GetColumnValue(row, columnIndex);
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<IReadOnlyList<string>> SortRowsNumerically(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int columnIndex,
        SortDirection direction)
    {
        IOrderedEnumerable<IReadOnlyList<string>> sorted = direction == SortDirection.Ascending
            ? rows.OrderBy(row => decimal.Parse(GetColumnValue(row, columnIndex), CultureInfo.InvariantCulture))
            : rows.OrderByDescending(row => decimal.Parse(GetColumnValue(row, columnIndex), CultureInfo.InvariantCulture));

        return sorted;
    }

    private static IEnumerable<IReadOnlyList<string>> SortRowsAsText(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int columnIndex,
        SortDirection direction)
    {
        IOrderedEnumerable<IReadOnlyList<string>> sorted = direction == SortDirection.Ascending
            ? rows.OrderBy(row => GetColumnValue(row, columnIndex), StringComparer.Ordinal)
            : rows.OrderByDescending(row => GetColumnValue(row, columnIndex), StringComparer.Ordinal);

        return sorted;
    }

    private static bool TryParseAggregate(
        IReadOnlyList<string> aggregateArguments,
        TextWriter error,
        out AggregateSpecification specification)
    {
        if (aggregateArguments.Count == 1 && string.Equals(aggregateArguments[0], "count", StringComparison.Ordinal))
        {
            specification = new AggregateSpecification(AggregateKind.Count, null);
            return true;
        }

        if (aggregateArguments.Count == 2
            && string.Equals(aggregateArguments[0], "sum", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(aggregateArguments[1]))
        {
            specification = new AggregateSpecification(AggregateKind.Sum, aggregateArguments[1]);
            return true;
        }

        error.WriteLine("Invalid groupby expression.");
        specification = default;
        return false;
    }

    private static bool TrySumColumn(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int columnIndex,
        out decimal sum,
        out string? invalidValue)
    {
        sum = 0m;
        invalidValue = null;

        foreach (IReadOnlyList<string> row in rows)
        {
            string value = GetColumnValue(row, columnIndex);
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsedValue))
            {
                invalidValue = value;
                return false;
            }

            sum += parsedValue;
        }

        return true;
    }

    private readonly record struct ComparisonFilter(
        string ColumnName,
        ComparisonOperator Operator,
        string LiteralValue,
        bool LiteralIsNumeric,
        decimal LiteralNumericValue);

    private readonly record struct SortSpecification(string ColumnName, SortDirection Direction);

    private readonly record struct AggregateSpecification(AggregateKind Kind, string? ColumnName);

    private enum ComparisonOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual
    }

    private enum SortDirection
    {
        Ascending,
        Descending
    }

    private enum AggregateKind
    {
        Count,
        Sum
    }

    private sealed class GroupAggregateState
    {
        public int Count { get; set; }

        public decimal Sum { get; set; }
    }
}
