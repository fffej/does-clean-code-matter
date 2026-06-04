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
        if (!TryParseCommandSequence(args, out var path, out var commands))
        {
            WriteUsage(stderr);
            return 1;
        }

        try
        {
            if (commands.Count == 1 && commands[0].Kind is PipelineCommandKind.Select or PipelineCommandKind.Where or PipelineCommandKind.Sort or PipelineCommandKind.Head or PipelineCommandKind.Distinct)
            {
                ExecuteSingleCommand(path, commands[0], outputStream);
            }
            else
            {
                ExecuteCommandSequence(path, commands, outputStream);
            }

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private static bool TryParseCommandSequence(string[] args, out string path, out List<PipelineCommand> commands)
    {
        path = string.Empty;
        commands = new List<PipelineCommand>();

        if (args.Length < 2)
        {
            return false;
        }

        path = args[0];
        var index = 1;

        while (index < args.Length)
        {
            var token = args[index];

            if (IsCommand(token, "select"))
            {
                if (index + 1 >= args.Length)
                {
                    return false;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Select, args[index + 1]));
                index += 2;
            }
            else if (IsCommand(token, "where"))
            {
                if (index + 1 >= args.Length)
                {
                    return false;
                }

                var expressionStart = index + 1;
                index = expressionStart;
                while (index < args.Length && !IsCommandKeyword(args[index]))
                {
                    index++;
                }

                if (index == expressionStart)
                {
                    return false;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Where, string.Join(' ', args[expressionStart..index])));
            }
            else if (IsCommand(token, "sort"))
            {
                if (index + 1 >= args.Length)
                {
                    return false;
                }

                var columnName = args[index + 1];
                var direction = "asc";
                index += 2;

                if (index < args.Length && !IsCommandKeyword(args[index]))
                {
                    direction = args[index];
                    index++;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Sort, columnName, direction));
            }
            else if (IsCommand(token, "head"))
            {
                if (index + 1 >= args.Length)
                {
                    return false;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Head, args[index + 1]));
                index += 2;
            }
            else if (IsCommand(token, "distinct"))
            {
                if (index + 1 >= args.Length)
                {
                    return false;
                }

                var columnsStart = index + 1;
                index = columnsStart;
                while (index < args.Length && !IsCommandKeyword(args[index]))
                {
                    index++;
                }

                if (index == columnsStart)
                {
                    return false;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Distinct, string.Join(' ', args[columnsStart..index])));
            }
            else if (IsCommand(token, "count"))
            {
                commands.Add(new PipelineCommand(PipelineCommandKind.Count));
                index++;
                return index == args.Length;
            }
            else if (IsCommand(token, "sum"))
            {
                if (index + 1 >= args.Length)
                {
                    return false;
                }

                commands.Add(new PipelineCommand(PipelineCommandKind.Sum, args[index + 1]));
                index += 2;
                return index == args.Length;
            }
            else if (IsCommand(token, "groupby"))
            {
                if (index + 2 >= args.Length)
                {
                    return false;
                }

                var groupColumnName = args[index + 1];
                var aggregateName = args[index + 2];

                if (IsCommand(aggregateName, "count"))
                {
                    commands.Add(new PipelineCommand(PipelineCommandKind.GroupByCount, groupColumnName));
                    index += 3;
                    return index == args.Length;
                }

                if (IsCommand(aggregateName, "sum"))
                {
                    if (index + 3 >= args.Length)
                    {
                        return false;
                    }

                    commands.Add(new PipelineCommand(PipelineCommandKind.GroupBySum, groupColumnName, args[index + 3]));
                    index += 4;
                    return index == args.Length;
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

    private static void ExecuteSingleCommand(string path, PipelineCommand command, Stream outputStream)
    {
        switch (command.Kind)
        {
            case PipelineCommandKind.Select:
            {
                var selectedColumns = command.FirstArgument
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                CsvRoundTripper.WriteSelectedColumns(path, selectedColumns, outputStream);
                break;
            }
            case PipelineCommandKind.Where:
                CsvRoundTripper.WriteFilteredRows(path, command.FirstArgument, outputStream);
                break;
            case PipelineCommandKind.Sort:
                CsvRoundTripper.WriteSortedRows(path, command.FirstArgument, command.SecondArgument ?? "asc", outputStream);
                break;
            case PipelineCommandKind.Head:
                if (!int.TryParse(command.FirstArgument, out var rowCount))
                {
                    throw new InvalidOperationException("Row count must be a positive integer");
                }

                CsvRoundTripper.WriteHeadRows(path, rowCount, outputStream);
                break;
            case PipelineCommandKind.Distinct:
            {
                var distinctColumns = command.FirstArgument
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                CsvRoundTripper.WriteDistinctRows(path, distinctColumns, outputStream);
                break;
            }
            default:
                ExecuteCommandSequence(path, [command], outputStream);
                break;
        }
    }

    private static void ExecuteCommandSequence(string path, IReadOnlyList<PipelineCommand> commands, Stream outputStream)
    {
        var table = ReadTable(path);

        foreach (var command in commands)
        {
            switch (command.Kind)
            {
                case PipelineCommandKind.Select:
                    table = SelectColumns(table, command.FirstArgument);
                    break;
                case PipelineCommandKind.Where:
                    table = FilterRows(table, command.FirstArgument);
                    break;
                case PipelineCommandKind.Sort:
                    table = SortRows(table, command.FirstArgument, command.SecondArgument ?? "asc");
                    break;
                case PipelineCommandKind.Head:
                    if (!int.TryParse(command.FirstArgument, out var rowCount))
                    {
                        throw new InvalidOperationException("Row count must be a positive integer");
                    }

                    table = HeadRows(table, rowCount);
                    break;
                case PipelineCommandKind.Distinct:
                    table = DistinctRows(table, command.FirstArgument);
                    break;
                case PipelineCommandKind.Count:
                    WriteAggregateCount(table, outputStream);
                    return;
                case PipelineCommandKind.Sum:
                    WriteAggregateSum(table, command.FirstArgument, outputStream);
                    return;
                case PipelineCommandKind.GroupByCount:
                    WriteGroupedCount(table, command.FirstArgument, outputStream);
                    return;
                case PipelineCommandKind.GroupBySum:
                    WriteGroupedSum(table, command.FirstArgument, command.SecondArgument ?? string.Empty, outputStream);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        WriteTable(table, outputStream);
    }

    private static void WriteAggregateCount(string path, Stream outputStream)
    {
        WriteAggregateCount(ReadTable(path), outputStream);
    }

    private static void WriteAggregateCount(CsvTable table, Stream outputStream)
    {
        using var writer = new StreamWriter(outputStream, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        writer.WriteLine(table.Rows.Count);
        writer.Flush();
    }

    private static void WriteAggregateSum(string path, string columnName, Stream outputStream)
    {
        WriteAggregateSum(ReadTable(path), columnName, outputStream);
    }

    private static void WriteAggregateSum(CsvTable table, string columnName, Stream outputStream)
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
            if (!decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var numericValue))
            {
                throw new InvalidOperationException("All values in the target column must be numeric");
            }

            total += numericValue;
        }

        using var writer = new StreamWriter(outputStream, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        writer.WriteLine(total.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.Flush();
    }

    private static void WriteGroupedCount(CsvTable table, string groupColumnName, Stream outputStream)
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

        using var writer = new StreamWriter(outputStream, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        writer.WriteLine(string.Join(",", new[] { groupColumnName, "count" }));
        foreach (var groupValue in groupOrder)
        {
            writer.WriteLine(string.Join(",", new[] { groupValue, groupCounts[groupValue].ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }

        writer.Flush();
    }

    private static void WriteGroupedSum(CsvTable table, string groupColumnName, string valueColumnName, Stream outputStream)
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
            if (!decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var numericValue))
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

        using var writer = new StreamWriter(outputStream, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        writer.WriteLine(string.Join(",", new[] { groupColumnName, "sum" }));
        foreach (var groupValue in groupOrder)
        {
            writer.WriteLine(string.Join(",", new[] { groupValue, groupTotals[groupValue].ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }

        writer.Flush();
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

    private static void WriteTable(CsvTable table, Stream outputStream)
    {
        using var writer = new StreamWriter(outputStream, leaveOpen: true)
        {
            NewLine = Environment.NewLine
        };

        writer.WriteLine(string.Join(",", table.Headers));
        foreach (var row in table.Rows)
        {
            writer.WriteLine(string.Join(",", row));
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
        stderr.WriteLine("Usage: slice <csv-file> select <column1,column2,...>");
        stderr.WriteLine("   or: slice <csv-file> where <column><operator><value>");
        stderr.WriteLine("   or: slice <csv-file> sort <column> [asc|desc]");
        stderr.WriteLine("   or: slice <csv-file> head <positive-integer>");
        stderr.WriteLine("   or: slice <csv-file> distinct <column1> [column2 ...]");
        stderr.WriteLine("   or: slice <csv-file> count");
        stderr.WriteLine("   or: slice <csv-file> sum <column>");
        stderr.WriteLine("   or: slice <csv-file> groupby <column> count");
        stderr.WriteLine("   or: slice <csv-file> groupby <column> sum <column>");
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
