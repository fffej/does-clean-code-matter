using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Slice;

internal static class Program
{
    public static int Main(string[] args)
    {
        return SliceApp.Run(args, Console.Error, Console.OpenStandardOutput());
    }
}

public static class SliceApp
{
    public static int Run(string[] args, TextWriter stderr, Stream outputStream)
    {
        if (!TryParseCommandSequence(args, out var path, out var commands, out var format))
        {
            WriteUsage(stderr);
            return 1;
        }

        try
        {
            ExecuteCommandSequence(path, commands, format, outputStream);

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private static bool TryParseCommandSequence(string[] args, out string path, out List<PipelineCommand> commands, out OutputFormat format)
    {
        path = string.Empty;
        commands = new List<PipelineCommand>();
        format = OutputFormat.Csv;

        if (args.Length < 2)
        {
            return false;
        }

        var filteredArgs = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            if (IsOption(args[i], "--format"))
            {
                if (i + 1 >= args.Length || !TryParseOutputFormat(args[i + 1], out format))
                {
                    return false;
                }

                i++;
                continue;
            }

            filteredArgs.Add(args[i]);
        }

        if (filteredArgs.Count < 2)
        {
            return false;
        }

        path = filteredArgs[0];
        var index = 1;

        while (index < filteredArgs.Count)
        {
            var token = filteredArgs[index];

            if (IsPipelineSeparator(token))
            {
                index++;
                continue;
            }

            if (IsCommand(token, "select"))
            {
                if (index + 1 >= filteredArgs.Count)
                {
                    return false;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Select, filteredArgs[index + 1]));
                index += 2;
            }
            else if (IsCommand(token, "where"))
            {
                if (index + 1 >= filteredArgs.Count)
                {
                    return false;
                }

                var expressionStart = index + 1;
                index = expressionStart;
                while (index < filteredArgs.Count && !IsCommandKeyword(filteredArgs[index]) && !IsPipelineSeparator(filteredArgs[index]))
                {
                    index++;
                }

                if (index == expressionStart)
                {
                    return false;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Where, string.Join(' ', filteredArgs.GetRange(expressionStart, index - expressionStart))));
            }
            else if (IsCommand(token, "sort"))
            {
                if (index + 1 >= filteredArgs.Count)
                {
                    return false;
                }

                var columnName = filteredArgs[index + 1];
                var direction = "asc";
                index += 2;

                if (index < filteredArgs.Count && !IsCommandKeyword(filteredArgs[index]) && !IsPipelineSeparator(filteredArgs[index]))
                {
                    direction = filteredArgs[index];
                    index++;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Sort, columnName, direction));
            }
            else if (IsCommand(token, "head"))
            {
                if (index + 1 >= filteredArgs.Count)
                {
                    return false;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Head, filteredArgs[index + 1]));
                index += 2;
            }
            else if (IsCommand(token, "distinct"))
            {
                if (index + 1 >= filteredArgs.Count)
                {
                    return false;
                }

                var columnsStart = index + 1;
                index = columnsStart;
                while (index < filteredArgs.Count && !IsCommandKeyword(filteredArgs[index]) && !IsPipelineSeparator(filteredArgs[index]))
                {
                    index++;
                }

                if (index == columnsStart)
                {
                    return false;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Distinct, string.Join(' ', filteredArgs.GetRange(columnsStart, index - columnsStart))));
            }
            else if (IsCommand(token, "count"))
            {
                commands.Add(new PipelineCommand(PipelineCommandKind.Count));
                index++;
            }
            else if (IsCommand(token, "sum"))
            {
                if (index + 1 >= filteredArgs.Count)
                {
                    return false;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Sum, filteredArgs[index + 1]));
                index += 2;
            }
            else if (IsCommand(token, "groupby"))
            {
                if (index + 2 >= filteredArgs.Count)
                {
                    return false;
                }

                var groupColumnName = filteredArgs[index + 1];
                var aggregateName = filteredArgs[index + 2];

                if (IsCommand(aggregateName, "count"))
                {
                    commands.Add(new PipelineCommand(PipelineCommandKind.GroupByCount, groupColumnName));
                    index += 3;
                    continue;
                }

                if (IsCommand(aggregateName, "sum"))
                {
                    if (index + 3 >= filteredArgs.Count)
                    {
                        return false;
                    }

                    commands.Add(new PipelineCommand(PipelineCommandKind.GroupBySum, groupColumnName, filteredArgs[index + 3]));
                    index += 4;
                    continue;
                }

                return false;
            }
            else
            {
                return false;
            }
        }

        return commands.Count > 0;
    }

    private static void ExecuteCommandSequence(string path, IReadOnlyList<PipelineCommand> commands, OutputFormat format, Stream outputStream)
    {
        PipelineResult current = new TableResult(ReadTable(path));

        foreach (var command in commands)
        {
            current = current switch
            {
                TableResult tableResult => ExecuteTableCommand(tableResult.Table, command),
                ScalarResult => throw new InvalidOperationException("Cannot apply a row-based command after an aggregate result"),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        switch (current)
        {
            case TableResult tableResult:
                WriteTable(tableResult.Table, format, outputStream);
                break;
            case ScalarResult scalarResult:
                WriteScalar(scalarResult.Value, format, outputStream);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(current));
        }
    }

    private static PipelineResult ExecuteTableCommand(CsvTable table, PipelineCommand command)
    {
        return command.Kind switch
        {
            PipelineCommandKind.Select => new TableResult(SelectColumns(table, command.FirstArgument)),
            PipelineCommandKind.Where => new TableResult(FilterRows(table, command.FirstArgument)),
            PipelineCommandKind.Sort => new TableResult(SortRows(table, command.FirstArgument, command.SecondArgument ?? "asc")),
            PipelineCommandKind.Head => new TableResult(HeadRows(table, ParseRowCount(command.FirstArgument))),
            PipelineCommandKind.Distinct => new TableResult(DistinctRows(table, command.FirstArgument)),
            PipelineCommandKind.Count => new ScalarResult(table.Rows.Count),
            PipelineCommandKind.Sum => new ScalarResult(CalculateSum(table, command.FirstArgument)),
            PipelineCommandKind.GroupByCount => new TableResult(BuildGroupedCountTable(table, command.FirstArgument)),
            PipelineCommandKind.GroupBySum => new TableResult(BuildGroupedSumTable(table, command.FirstArgument, command.SecondArgument ?? string.Empty)),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
    }

    private static int ParseRowCount(string value)
    {
        if (!int.TryParse(value, out var rowCount))
        {
            throw new InvalidOperationException("Row count must be a positive integer");
        }

        return rowCount;
    }

    private static decimal CalculateSum(CsvTable table, string columnName)
    {
        var headerIndexes = BuildHeaderIndex(table.Headers);
        if (!headerIndexes.TryGetValue(columnName, out var columnIndex))
        {
            throw new InvalidOperationException($"Column not found: {columnName}");
        }

        decimal total = 0m;
        foreach (var row in table.Rows)
        {
            var value = GetValue(row, columnIndex);
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericValue))
            {
                throw new InvalidOperationException("All values in the target column must be numeric");
            }

            total += numericValue;
        }

        return total;
    }

    private static CsvTable BuildGroupedCountTable(CsvTable table, string groupColumnName)
    {
        var headerIndexes = BuildHeaderIndex(table.Headers);
        if (!headerIndexes.TryGetValue(groupColumnName, out var groupColumnIndex))
        {
            throw new InvalidOperationException($"Column not found: {groupColumnName}");
        }

        var groupOrder = new List<string>();
        var groupCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var row in table.Rows)
        {
            var groupValue = GetValue(row, groupColumnIndex);
            if (!groupCounts.ContainsKey(groupValue))
            {
                groupOrder.Add(groupValue);
                groupCounts[groupValue] = 0;
            }

            groupCounts[groupValue]++;
        }

        var rows = new List<string[]>(groupOrder.Count);
        foreach (var groupValue in groupOrder)
        {
            rows.Add(new[] { groupValue, groupCounts[groupValue].ToString(CultureInfo.InvariantCulture) });
        }

        return new CsvTable(new List<string> { groupColumnName, "count" }, rows);
    }

    private static CsvTable BuildGroupedSumTable(CsvTable table, string groupColumnName, string valueColumnName)
    {
        var headerIndexes = BuildHeaderIndex(table.Headers);
        if (!headerIndexes.TryGetValue(groupColumnName, out var groupColumnIndex))
        {
            throw new InvalidOperationException($"Column not found: {groupColumnName}");
        }

        if (!headerIndexes.TryGetValue(valueColumnName, out var valueColumnIndex))
        {
            throw new InvalidOperationException($"Column not found: {valueColumnName}");
        }

        var groupOrder = new List<string>();
        var groupTotals = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var row in table.Rows)
        {
            var groupValue = GetValue(row, groupColumnIndex);
            var value = GetValue(row, valueColumnIndex);
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericValue))
            {
                throw new InvalidOperationException("All values in the target column must be numeric");
            }

            if (!groupTotals.ContainsKey(groupValue))
            {
                groupOrder.Add(groupValue);
                groupTotals[groupValue] = 0m;
            }

            groupTotals[groupValue] += numericValue;
        }

        var rows = new List<string[]>(groupOrder.Count);
        foreach (var groupValue in groupOrder)
        {
            rows.Add(new[] { groupValue, groupTotals[groupValue].ToString(CultureInfo.InvariantCulture) });
        }

        return new CsvTable(new List<string> { groupColumnName, "sum" }, rows);
    }

    private static CsvTable ReadTable(string path)
    {
        using var reader = new StreamReader(File.OpenRead(path));

        var headerLine = reader.ReadLine() ?? throw new InvalidOperationException("CSV file is empty");
        var headers = ParseCsvLine(headerLine);
        var rows = new List<string[]>();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var values = ParseCsvLine(line);
            var normalized = new string[headers.Count];
            for (var i = 0; i < normalized.Length; i++)
            {
                normalized[i] = i < values.Count ? values[i] : string.Empty;
            }

            rows.Add(normalized);
        }

        return new CsvTable(headers, rows);
    }

    private static void WriteTable(CsvTable table, OutputFormat format, Stream outputStream)
    {
        using var writer = new StreamWriter(outputStream, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        switch (format)
        {
            case OutputFormat.Csv:
                writer.WriteLine(string.Join(",", table.Headers));
                foreach (var row in table.Rows)
                {
                    writer.WriteLine(string.Join(",", row));
                }

                break;
            case OutputFormat.Json:
            {
                var rows = new JsonArray();
                foreach (var row in table.Rows)
                {
                    var obj = new JsonObject();
                    for (var i = 0; i < table.Headers.Count; i++)
                    {
                        obj[table.Headers[i]] = GetValue(row, i);
                    }

                    rows.Add(obj);
                }

                writer.WriteLine(rows.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
                break;
            }
            case OutputFormat.Table:
            {
                var widths = new int[table.Headers.Count];
                for (var i = 0; i < table.Headers.Count; i++)
                {
                    widths[i] = table.Headers[i].Length;
                }

                foreach (var row in table.Rows)
                {
                    for (var i = 0; i < table.Headers.Count; i++)
                    {
                        widths[i] = Math.Max(widths[i], GetValue(row, i).Length);
                    }
                }

                writer.WriteLine(string.Join(" | ", table.Headers.Select((header, index) => header.PadRight(widths[index]))));
                writer.WriteLine(string.Join(" | ", widths.Select(width => new string('-', width))));
                foreach (var row in table.Rows)
                {
                    writer.WriteLine(string.Join(" | ", table.Headers.Select((_, index) => GetValue(row, index).PadRight(widths[index]))));
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }

        writer.Flush();
    }

    private static void WriteScalar(object value, OutputFormat format, Stream outputStream)
    {
        using var writer = new StreamWriter(outputStream, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        switch (format)
        {
            case OutputFormat.Json:
                writer.WriteLine(JsonSerializer.Serialize(value));
                break;
            case OutputFormat.Csv:
            case OutputFormat.Table:
                writer.WriteLine(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }

        writer.Flush();
    }

    private static CsvTable SelectColumns(CsvTable table, string columnsExpression)
    {
        var selectedColumns = columnsExpression
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (selectedColumns.Length == 0)
        {
            throw new InvalidOperationException("At least one column must be specified");
        }

        var headerIndexes = BuildHeaderIndex(table.Headers);
        var selectedIndexes = selectedColumns
            .Select(columnName => headerIndexes.TryGetValue(columnName, out var index)
                ? index
                : throw new InvalidOperationException($"Column not found: {columnName}"))
            .ToArray();

        var rows = new List<string[]>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            var projected = new string[selectedIndexes.Length];
            for (var i = 0; i < selectedIndexes.Length; i++)
            {
                projected[i] = GetValue(row, selectedIndexes[i]);
            }

            rows.Add(projected);
        }

        return new CsvTable(selectedColumns.ToList(), rows);
    }

    private static CsvTable FilterRows(CsvTable table, string expression)
    {
        var filter = ParseComparisonExpression(expression);
        var headerIndexes = BuildHeaderIndex(table.Headers);
        if (!headerIndexes.TryGetValue(filter.ColumnName, out var filterColumnIndex))
        {
            throw new InvalidOperationException($"Column not found: {filter.ColumnName}");
        }

        var rows = new List<string[]>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            if (Matches(GetValue(row, filterColumnIndex), filter.Operator, filter.Value))
            {
                rows.Add((string[])row.Clone());
            }
        }

        return new CsvTable(new List<string>(table.Headers), rows);
    }

    private static CsvTable SortRows(CsvTable table, string columnName, string direction)
    {
        var descending = ParseSortDirection(direction);
        var headerIndexes = BuildHeaderIndex(table.Headers);
        if (!headerIndexes.TryGetValue(columnName, out var sortColumnIndex))
        {
            throw new InvalidOperationException($"Column not found: {columnName}");
        }

        var rows = new List<SortableRow>();
        var rowIndex = 0;
        var allNumeric = true;
        foreach (var row in table.Rows)
        {
            var sortValue = GetValue(row, sortColumnIndex);
            if (!TryParseNumber(sortValue, out var numericSortValue))
            {
                allNumeric = false;
            }

            rows.Add(new SortableRow(row, sortValue, numericSortValue, rowIndex++));
        }

        IEnumerable<SortableRow> orderedRows = allNumeric
            ? descending
                ? rows.OrderByDescending(row => row.NumericSortValue).ThenBy(row => row.OriginalIndex)
                : rows.OrderBy(row => row.NumericSortValue).ThenBy(row => row.OriginalIndex)
            : descending
                ? rows.OrderByDescending(row => row.SortValue, StringComparer.Ordinal).ThenBy(row => row.OriginalIndex)
                : rows.OrderBy(row => row.SortValue, StringComparer.Ordinal).ThenBy(row => row.OriginalIndex);

        return new CsvTable(new List<string>(table.Headers), orderedRows.Select(row => row.Values).ToList());
    }

    private static CsvTable HeadRows(CsvTable table, int rowCount)
    {
        if (rowCount <= 0)
        {
            throw new InvalidOperationException("Row count must be a positive integer");
        }

        return new CsvTable(new List<string>(table.Headers), table.Rows.Take(rowCount).Select(row => (string[])row.Clone()).ToList());
    }

    private static CsvTable DistinctRows(CsvTable table, string columnsExpression)
    {
        var distinctColumns = columnsExpression
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (distinctColumns.Length == 0)
        {
            throw new InvalidOperationException("At least one column must be specified");
        }

        var headerIndexes = BuildHeaderIndex(table.Headers);
        var distinctIndexes = distinctColumns
            .Select(columnName => headerIndexes.TryGetValue(columnName, out var index)
                ? index
                : throw new InvalidOperationException($"Column not found: {columnName}"))
            .ToArray();

        var rows = new List<string[]>(table.Rows.Count);
        var seenKeys = new HashSet<string[]>(new StringArrayComparer());
        foreach (var row in table.Rows)
        {
            var projected = distinctIndexes.Select(index => GetValue(row, index)).ToArray();
            if (seenKeys.Add(projected))
            {
                rows.Add(projected);
            }
        }

        return new CsvTable(distinctColumns.ToList(), rows);
    }

    private static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> headers)
    {
        return headers
            .Select((header, index) => new { header, index })
            .ToDictionary(x => x.header, x => x.index, StringComparer.Ordinal);
    }

    private static string GetValue(IReadOnlyList<string> values, int index)
    {
        return index < values.Count ? values[index] : string.Empty;
    }

    private static bool IsCommandKeyword(string value)
    {
        return IsCommand(value, "select")
            || IsCommand(value, "where")
            || IsCommand(value, "sort")
            || IsCommand(value, "head")
            || IsCommand(value, "distinct")
            || IsCommand(value, "count")
            || IsCommand(value, "sum")
            || IsCommand(value, "groupby");
    }

    private static bool IsCommand(string value, string command)
    {
        return string.Equals(value, command, StringComparison.Ordinal);
    }

    private static bool IsPipelineSeparator(string value)
    {
        return string.Equals(value, "|", StringComparison.Ordinal);
    }

    private static bool IsOption(string value, string option)
    {
        return string.Equals(value, option, StringComparison.Ordinal);
    }

    private static bool TryParseOutputFormat(string value, out OutputFormat format)
    {
        switch (value)
        {
            case "csv":
                format = OutputFormat.Csv;
                return true;
            case "json":
                format = OutputFormat.Json;
                return true;
            case "table":
                format = OutputFormat.Table;
                return true;
            default:
                format = OutputFormat.Csv;
                return false;
        }
    }

    private static bool Matches(string leftValue, ComparisonOperator op, string rightValue)
    {
        if (TryParseNumber(leftValue, out var leftNumber) && TryParseNumber(rightValue, out var rightNumber))
        {
            return op switch
            {
                ComparisonOperator.Equal => leftNumber == rightNumber,
                ComparisonOperator.NotEqual => leftNumber != rightNumber,
                ComparisonOperator.GreaterThan => leftNumber > rightNumber,
                ComparisonOperator.LessThan => leftNumber < rightNumber,
                ComparisonOperator.GreaterThanOrEqual => leftNumber >= rightNumber,
                ComparisonOperator.LessThanOrEqual => leftNumber <= rightNumber,
                _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
            };
        }

        var comparison = string.CompareOrdinal(leftValue, rightValue);
        return op switch
        {
            ComparisonOperator.Equal => comparison == 0,
            ComparisonOperator.NotEqual => comparison != 0,
            ComparisonOperator.GreaterThan => comparison > 0,
            ComparisonOperator.LessThan => comparison < 0,
            ComparisonOperator.GreaterThanOrEqual => comparison >= 0,
            ComparisonOperator.LessThanOrEqual => comparison <= 0,
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
        };
    }

    private static bool TryParseNumber(string value, out decimal number)
    {
        return decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out number);
    }

    private static bool ParseSortDirection(string direction)
    {
        return direction switch
        {
            "asc" => false,
            "desc" => true,
            _ => throw new InvalidOperationException("Invalid sort direction")
        };
    }

    private static ComparisonFilter ParseComparisonExpression(string expression)
    {
        var trimmed = expression.Trim();
        var operators = new[] { ">=", "<=", "!=", "=", ">", "<" };

        foreach (var opText in operators)
        {
            var operatorIndex = trimmed.IndexOf(opText, StringComparison.Ordinal);
            if (operatorIndex <= 0)
            {
                continue;
            }

            var columnName = trimmed[..operatorIndex].Trim();
            var value = trimmed[(operatorIndex + opText.Length)..].Trim();
            if (columnName.Length == 0 || value.Length == 0)
            {
                break;
            }

            return new ComparisonFilter(columnName, ParseOperator(opText), value);
        }

        throw new InvalidOperationException("Invalid comparison expression");
    }

    private static ComparisonOperator ParseOperator(string opText)
    {
        return opText switch
        {
            "=" => ComparisonOperator.Equal,
            "!=" => ComparisonOperator.NotEqual,
            ">" => ComparisonOperator.GreaterThan,
            "<" => ComparisonOperator.LessThan,
            ">=" => ComparisonOperator.GreaterThanOrEqual,
            "<=" => ComparisonOperator.LessThanOrEqual,
            _ => throw new InvalidOperationException("Invalid comparison operator")
        };
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == ',')
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private static void WriteUsage(TextWriter stderr)
    {
        stderr.WriteLine("Usage: slice <csv-file> [--format csv|json|table] <command> [| <command> ...]");
        stderr.WriteLine("Commands: select <column1,column2,...>");
        stderr.WriteLine("          where <column><operator><value>");
        stderr.WriteLine("          sort <column> [asc|desc]");
        stderr.WriteLine("          head <positive-integer>");
        stderr.WriteLine("          distinct <column1> [column2 ...]");
        stderr.WriteLine("          count");
        stderr.WriteLine("          sum <column>");
        stderr.WriteLine("          groupby <column> count");
        stderr.WriteLine("          groupby <column> sum <column>");
    }

    private sealed class CsvTable
    {
        public CsvTable(List<string> headers, List<string[]> rows)
        {
            Headers = headers;
            Rows = rows;
        }

        public List<string> Headers { get; }

        public List<string[]> Rows { get; }
    }

    private enum PipelineCommandKind
    {
        Select,
        Where,
        Sort,
        Head,
        Distinct,
        Count,
        Sum,
        GroupByCount,
        GroupBySum
    }

    private readonly record struct PipelineCommand(
        PipelineCommandKind Kind,
        string FirstArgument = "",
        string? SecondArgument = null);

    private sealed class StringArrayComparer : IEqualityComparer<string[]>
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
            var hash = new HashCode();
            foreach (var value in obj)
            {
                hash.Add(value, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }

    private readonly record struct ComparisonFilter(string ColumnName, ComparisonOperator Operator, string Value);

    private enum ComparisonOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual
    }

    private readonly record struct SortableRow(string[] Values, string SortValue, decimal NumericSortValue, int OriginalIndex);

    private enum OutputFormat
    {
        Csv,
        Json,
        Table
    }

    private abstract record PipelineResult;

    private sealed record TableResult(CsvTable Table) : PipelineResult;

    private sealed record ScalarResult(object Value) : PipelineResult;

}

public static class CsvRoundTripper
{
    public static void WriteSelectedColumns(string path, IReadOnlyList<string> selectedColumns, Stream output)
    {
        using var reader = new StreamReader(File.OpenRead(path));
        using var writer = new StreamWriter(output, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        var headerLine = reader.ReadLine() ?? throw new InvalidOperationException("CSV file is empty");
        var headers = ParseCsvLine(headerLine);
        var headerIndexes = headers
            .Select((header, index) => new { header, index })
            .ToDictionary(x => x.header, x => x.index, StringComparer.Ordinal);

        var selectedIndexes = selectedColumns
            .Select(columnName => headerIndexes.TryGetValue(columnName, out var index)
                ? index
                : throw new InvalidOperationException($"Column not found: {columnName}"))
            .ToArray();

        writer.WriteLine(string.Join(",", selectedColumns));

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                writer.WriteLine();
                continue;
            }

            var values = ParseCsvLine(line);
            var projectedValues = selectedIndexes.Select(index => index < values.Count ? values[index] : string.Empty);
            writer.WriteLine(string.Join(",", projectedValues));
        }

        writer.Flush();
    }

    public static void WriteFilteredRows(string path, string expression, Stream output)
    {
        var filter = ParseComparisonExpression(expression);

        using var reader = new StreamReader(File.OpenRead(path));
        using var writer = new StreamWriter(output, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        var headerLine = reader.ReadLine() ?? throw new InvalidOperationException("CSV file is empty");
        var headers = ParseCsvLine(headerLine);
        var headerIndexes = headers
            .Select((header, index) => new { header, index })
            .ToDictionary(x => x.header, x => x.index, StringComparer.Ordinal);

        if (!headerIndexes.TryGetValue(filter.ColumnName, out var filterColumnIndex))
        {
            throw new InvalidOperationException($"Column not found: {filter.ColumnName}");
        }

        writer.WriteLine(headerLine);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var values = ParseCsvLine(line);
            var leftValue = GetValue(values, filterColumnIndex);
            if (Matches(leftValue, filter.Operator, filter.Value))
            {
                writer.WriteLine(line);
            }
        }

        writer.Flush();
    }

    public static void WriteSortedRows(string path, string columnName, string direction, Stream output)
    {
        var descending = ParseSortDirection(direction);

        using var reader = new StreamReader(File.OpenRead(path));
        using var writer = new StreamWriter(output, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        var headerLine = reader.ReadLine() ?? throw new InvalidOperationException("CSV file is empty");
        var headers = ParseCsvLine(headerLine);
        var headerIndexes = headers
            .Select((header, index) => new { header, index })
            .ToDictionary(x => x.header, x => x.index, StringComparer.Ordinal);

        if (!headerIndexes.TryGetValue(columnName, out var sortColumnIndex))
        {
            throw new InvalidOperationException($"Column not found: {columnName}");
        }

        var rows = new List<SortableRow>();
        var rowIndex = 0;
        var allNumeric = true;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var values = ParseCsvLine(line);
            var sortValue = GetValue(values, sortColumnIndex);
            if (!TryParseNumber(sortValue, out var numericSortValue))
            {
                allNumeric = false;
            }

            rows.Add(new SortableRow(
                line,
                sortValue,
                numericSortValue,
                rowIndex++));
        }

        writer.WriteLine(headerLine);

        IEnumerable<SortableRow> orderedRows;
        if (allNumeric)
        {
            orderedRows = descending
                ? rows.OrderByDescending(row => row.NumericSortValue).ThenBy(row => row.OriginalIndex)
                : rows.OrderBy(row => row.NumericSortValue).ThenBy(row => row.OriginalIndex);
        }
        else
        {
            orderedRows = descending
                ? rows.OrderByDescending(row => row.SortValue, StringComparer.Ordinal).ThenBy(row => row.OriginalIndex)
                : rows.OrderBy(row => row.SortValue, StringComparer.Ordinal).ThenBy(row => row.OriginalIndex);
        }

        foreach (var row in orderedRows)
        {
            writer.WriteLine(row.Line);
        }

        writer.Flush();
    }

    public static void WriteHeadRows(string path, int rowCount, Stream output)
    {
        if (rowCount <= 0)
        {
            throw new InvalidOperationException("Row count must be a positive integer");
        }

        using var reader = new StreamReader(File.OpenRead(path));
        using var writer = new StreamWriter(output, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        var headerLine = reader.ReadLine() ?? throw new InvalidOperationException("CSV file is empty");
        writer.WriteLine(headerLine);

        var writtenRows = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null && writtenRows < rowCount)
        {
            if (line.Length == 0)
            {
                continue;
            }

            writer.WriteLine(line);
            writtenRows++;
        }

        writer.Flush();
    }

    public static void WriteDistinctRows(string path, IReadOnlyList<string> distinctColumns, Stream output)
    {
        if (distinctColumns.Count == 0)
        {
            throw new InvalidOperationException("At least one column must be specified");
        }

        using var reader = new StreamReader(File.OpenRead(path));
        using var writer = new StreamWriter(output, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        var headerLine = reader.ReadLine() ?? throw new InvalidOperationException("CSV file is empty");
        var headers = ParseCsvLine(headerLine);
        var headerIndexes = headers
            .Select((header, index) => new { header, index })
            .ToDictionary(x => x.header, x => x.index, StringComparer.Ordinal);

        var distinctIndexes = distinctColumns
            .Select(columnName => headerIndexes.TryGetValue(columnName, out var index)
                ? index
                : throw new InvalidOperationException($"Column not found: {columnName}"))
            .ToArray();

        writer.WriteLine(string.Join(",", distinctColumns));

        var seenKeys = new HashSet<string[]>(new StringArrayComparer());
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var values = ParseCsvLine(line);
            var key = distinctIndexes.Select(index => GetValue(values, index)).ToArray();
            if (!seenKeys.Add(key))
            {
                continue;
            }

            var projectedValues = distinctIndexes.Select(index => GetValue(values, index));
            writer.WriteLine(string.Join(",", projectedValues));
        }

        writer.Flush();
    }

    private static string GetValue(IReadOnlyList<string> values, int index)
    {
        return index < values.Count ? values[index] : string.Empty;
    }

    private static bool Matches(string leftValue, ComparisonOperator op, string rightValue)
    {
        if (TryParseNumber(leftValue, out var leftNumber) && TryParseNumber(rightValue, out var rightNumber))
        {
            return op switch
            {
                ComparisonOperator.Equal => leftNumber == rightNumber,
                ComparisonOperator.NotEqual => leftNumber != rightNumber,
                ComparisonOperator.GreaterThan => leftNumber > rightNumber,
                ComparisonOperator.LessThan => leftNumber < rightNumber,
                ComparisonOperator.GreaterThanOrEqual => leftNumber >= rightNumber,
                ComparisonOperator.LessThanOrEqual => leftNumber <= rightNumber,
                _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
            };
        }

        var comparison = string.CompareOrdinal(leftValue, rightValue);
        return op switch
        {
            ComparisonOperator.Equal => comparison == 0,
            ComparisonOperator.NotEqual => comparison != 0,
            ComparisonOperator.GreaterThan => comparison > 0,
            ComparisonOperator.LessThan => comparison < 0,
            ComparisonOperator.GreaterThanOrEqual => comparison >= 0,
            ComparisonOperator.LessThanOrEqual => comparison <= 0,
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
        };
    }

    private static bool TryParseNumber(string value, out decimal number)
    {
        return decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out number);
    }

    private static bool ParseSortDirection(string direction)
    {
        return direction switch
        {
            "asc" => false,
            "desc" => true,
            _ => throw new InvalidOperationException("Invalid sort direction")
        };
    }

    private static ComparisonFilter ParseComparisonExpression(string expression)
    {
        var trimmed = expression.Trim();
        var operators = new[] { ">=", "<=", "!=", "=", ">", "<" };

        foreach (var opText in operators)
        {
            var operatorIndex = trimmed.IndexOf(opText, StringComparison.Ordinal);
            if (operatorIndex <= 0)
            {
                continue;
            }

            var columnName = trimmed[..operatorIndex].Trim();
            var value = trimmed[(operatorIndex + opText.Length)..].Trim();
            if (columnName.Length == 0 || value.Length == 0)
            {
                break;
            }

            return new ComparisonFilter(columnName, ParseOperator(opText), value);
        }

        throw new InvalidOperationException("Invalid comparison expression");
    }

    private static ComparisonOperator ParseOperator(string opText)
    {
        return opText switch
        {
            "=" => ComparisonOperator.Equal,
            "!=" => ComparisonOperator.NotEqual,
            ">" => ComparisonOperator.GreaterThan,
            "<" => ComparisonOperator.LessThan,
            ">=" => ComparisonOperator.GreaterThanOrEqual,
            "<=" => ComparisonOperator.LessThanOrEqual,
            _ => throw new InvalidOperationException("Invalid comparison operator")
        };
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == ',')
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private sealed class StringArrayComparer : IEqualityComparer<string[]>
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
            var hash = new HashCode();
            foreach (var value in obj)
            {
                hash.Add(value, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }

    private enum ComparisonOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual
    }

    private readonly record struct ComparisonFilter(string ColumnName, ComparisonOperator Operator, string Value);

    private readonly record struct SortableRow(string Line, string SortValue, decimal NumericSortValue, int OriginalIndex);
}
