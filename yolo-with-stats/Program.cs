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
        if (args.Length < 3)
        {
            WriteUsage(stderr);
            return 1;
        }

        try
        {
            if (string.Equals(args[1], "select", StringComparison.Ordinal))
            {
                if (args.Length != 3)
                {
                    WriteUsage(stderr);
                    return 1;
                }

                var selectedColumns = args[2]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                CsvRoundTripper.WriteSelectedColumns(args[0], selectedColumns, outputStream);
            }
            else if (string.Equals(args[1], "where", StringComparison.Ordinal))
            {
                var expression = string.Join(' ', args.Skip(2));
                CsvRoundTripper.WriteFilteredRows(args[0], expression, outputStream);
            }
            else if (string.Equals(args[1], "sort", StringComparison.Ordinal))
            {
                if (args.Length is not 3 and not 4)
                {
                    WriteUsage(stderr);
                    return 1;
                }

                var columnName = args[2];
                var direction = args.Length == 4 ? args[3] : "asc";
                CsvRoundTripper.WriteSortedRows(args[0], columnName, direction, outputStream);
            }
            else if (string.Equals(args[1], "head", StringComparison.Ordinal))
            {
                if (args.Length != 3)
                {
                    WriteUsage(stderr);
                    return 1;
                }

                if (!int.TryParse(args[2], out var rowCount))
                {
                    throw new InvalidOperationException("Row count must be a positive integer");
                }

                CsvRoundTripper.WriteHeadRows(args[0], rowCount, outputStream);
            }
            else if (string.Equals(args[1], "distinct", StringComparison.Ordinal))
            {
                if (args.Length < 3)
                {
                    WriteUsage(stderr);
                    return 1;
                }

                var distinctColumns = args.Skip(2).ToArray();
                CsvRoundTripper.WriteDistinctRows(args[0], distinctColumns, outputStream);
            }
            else
            {
                WriteUsage(stderr);
                return 1;
            }

            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void WriteUsage(TextWriter stderr)
    {
        stderr.WriteLine("Usage: slice <csv-file> select <column1,column2,...>");
        stderr.WriteLine("   or: slice <csv-file> where <column><operator><value>");
        stderr.WriteLine("   or: slice <csv-file> sort <column> [asc|desc]");
        stderr.WriteLine("   or: slice <csv-file> head <positive-integer>");
        stderr.WriteLine("   or: slice <csv-file> distinct <column1> [column2 ...]");
    }
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
