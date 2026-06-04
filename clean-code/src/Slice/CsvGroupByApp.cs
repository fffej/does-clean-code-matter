using System.Globalization;

namespace Slice;

public static class CsvGroupByApp
{
    public static QueryResult? Run(string csvPath, string groupColumn, string[] aggregateArguments, TextWriter error)
    {
        if (!TryParseAggregate(aggregateArguments, error, out AggregateSpecification specification))
        {
            return null;
        }

        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return null;
        }

        IReadOnlyList<string> header = rows[0];
        int groupColumnIndex = CsvHeaderLookup.FindHeaderIndex(header, groupColumn);
        if (groupColumnIndex < 0)
        {
            error.WriteLine($"Column not found: {groupColumn}");
            return null;
        }

        int aggregateColumnIndex = -1;
        if (specification.Kind == AggregateKind.Sum)
        {
            aggregateColumnIndex = CsvHeaderLookup.FindHeaderIndex(header, specification.ColumnName!);
            if (aggregateColumnIndex < 0)
            {
                error.WriteLine($"Column not found: {specification.ColumnName}");
                return null;
            }
        }

        Dictionary<string, GroupAggregateState> aggregatesByGroup = new(StringComparer.Ordinal);
        List<string> groupOrder = [];

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            IReadOnlyList<string> row = rows[rowIndex];
            string groupValue = GetColumnValue(row, groupColumnIndex);

            if (!aggregatesByGroup.TryGetValue(groupValue, out GroupAggregateState? state))
            {
                state = new GroupAggregateState();
                aggregatesByGroup.Add(groupValue, state);
                groupOrder.Add(groupValue);
            }

            GroupAggregateState currentState = state;

            if (specification.Kind == AggregateKind.Count)
            {
                currentState.Count++;
                continue;
            }

            string value = GetColumnValue(row, aggregateColumnIndex);
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsedValue))
            {
                error.WriteLine($"Non-numeric value found in column {specification.ColumnName}: {value}");
                return null;
            }

            currentState.Sum += parsedValue;
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

        return new QueryResult.Table([groupColumn, aggregateHeader], resultRows);
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

    private static string GetColumnValue(IReadOnlyList<string> row, int columnIndex)
    {
        return columnIndex < row.Count ? row[columnIndex] : string.Empty;
    }

    private readonly record struct AggregateSpecification(AggregateKind Kind, string? ColumnName);

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
