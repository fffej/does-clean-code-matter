namespace Slice;

public static class App
{
    private const string Usage =
        "Usage: slice <csv-file> <command> [args...]";

    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        _ = input;

        if (args.Length < 2)
        {
            error.WriteLine(Usage);
            return 1;
        }

        string path = args[0];

        if (!File.Exists(path))
        {
            error.WriteLine($"File not found: {path}");
            return 1;
        }

        string csv = File.ReadAllText(path);

        if (!CsvDocument.TryParse(csv, out CsvDocument? parsedDocument, out string parseError))
        {
            error.WriteLine(parseError);
            return 1;
        }

        CsvDocument document = parsedDocument ?? throw new InvalidOperationException("CSV parser returned no document.");

        if (document.Header.Count == 0)
        {
            error.WriteLine("Input CSV is missing a header row.");
            return 1;
        }

        CsvDocument currentDocument = document;
        int argumentIndex = 1;

        while (argumentIndex < args.Length)
        {
            string command = args[argumentIndex++];

            if (string.Equals(command, "select", StringComparison.Ordinal))
            {
                if (argumentIndex >= args.Length)
                {
                    error.WriteLine(Usage);
                    return 1;
                }

                string argument = args[argumentIndex++];
                string[] requestedColumns = argument
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                if (requestedColumns.Length == 0)
                {
                    error.WriteLine("No columns selected.");
                    return 1;
                }

                if (!TryBuildSelection(currentDocument.Header, requestedColumns, out int[] selectedIndexes, out string missingColumn))
                {
                    error.WriteLine($"Column not found: {missingColumn}");
                    return 1;
                }

                IReadOnlyList<IReadOnlyList<string>> projectedRows = currentDocument.Rows
                    .Select(row => ProjectRow(row, selectedIndexes))
                    .ToArray();

                currentDocument = new CsvDocument(
                    selectedIndexes.Select(index => currentDocument.Header[index]).ToArray(),
                    projectedRows,
                    currentDocument.LineEnding,
                    currentDocument.EndsWithLineEnding);
                continue;
            }

            if (string.Equals(command, "where", StringComparison.Ordinal))
            {
                if (argumentIndex >= args.Length)
                {
                    error.WriteLine(Usage);
                    return 1;
                }

                string argument = args[argumentIndex++];
                if (!TryParseWhereExpression(argument, out string columnName, out ComparisonOperator comparisonOperator, out string literalValue, out string whereError))
                {
                    error.WriteLine(whereError);
                    return 1;
                }

                IReadOnlyDictionary<string, int> columnLookup = BuildColumnLookup(currentDocument.Header);
                if (!columnLookup.TryGetValue(columnName, out int columnIndex))
                {
                    error.WriteLine($"Column not found: {columnName}");
                    return 1;
                }

                var filteredRows = new List<IReadOnlyList<string>>();
                foreach (IReadOnlyList<string> row in currentDocument.Rows)
                {
                    string leftValue = columnIndex < row.Count ? row[columnIndex] : string.Empty;
                    if (MatchesComparison(leftValue, literalValue, comparisonOperator))
                    {
                        filteredRows.Add(row);
                    }
                }

                currentDocument = new CsvDocument(
                    currentDocument.Header.ToArray(),
                    filteredRows,
                    currentDocument.LineEnding,
                    currentDocument.EndsWithLineEnding);
                continue;
            }

            if (string.Equals(command, "sort", StringComparison.Ordinal))
            {
                if (argumentIndex >= args.Length)
                {
                    error.WriteLine(Usage);
                    return 1;
                }

                string argument = args[argumentIndex++];
                string? optionalArgument = argumentIndex < args.Length && IsSortDirection(args[argumentIndex])
                    ? args[argumentIndex]
                    : null;
                if (!TryParseSortDirection(optionalArgument, out bool descending, out string sortError))
                {
                    error.WriteLine(sortError);
                    return 1;
                }

                if (optionalArgument is not null)
                {
                    argumentIndex++;
                }

                if (!TrySortDocument(currentDocument, argument, descending, out IReadOnlyList<IReadOnlyList<string>> sortedRows, out string sortColumnError))
                {
                    error.WriteLine(sortColumnError);
                    return 1;
                }

                currentDocument = new CsvDocument(
                    currentDocument.Header.ToArray(),
                    sortedRows,
                    currentDocument.LineEnding,
                    currentDocument.EndsWithLineEnding);
                continue;
            }

            if (string.Equals(command, "head", StringComparison.Ordinal))
            {
                if (argumentIndex >= args.Length)
                {
                    error.WriteLine(Usage);
                    return 1;
                }

                string argument = args[argumentIndex++];
                if (!int.TryParse(argument, out int rowCount) || rowCount <= 0)
                {
                    error.WriteLine("Invalid row count.");
                    return 1;
                }

                int rowsToKeep = Math.Min(rowCount, currentDocument.Rows.Count);
                IReadOnlyList<IReadOnlyList<string>> limitedRows = currentDocument.Rows.Take(rowsToKeep).ToArray();
                currentDocument = new CsvDocument(
                    currentDocument.Header.ToArray(),
                    limitedRows,
                    currentDocument.LineEnding,
                    currentDocument.EndsWithLineEnding);
                continue;
            }

            if (string.Equals(command, "distinct", StringComparison.Ordinal))
            {
                if (argumentIndex >= args.Length)
                {
                    error.WriteLine(Usage);
                    return 1;
                }

                int distinctArgumentStart = argumentIndex;
                while (argumentIndex < args.Length && !IsCommandToken(args[argumentIndex]))
                {
                    argumentIndex++;
                }

                string[] requestedColumns = args[distinctArgumentStart..argumentIndex];
                if (requestedColumns.Length == 0)
                {
                    error.WriteLine("No columns selected.");
                    return 1;
                }

                if (!TryBuildSelection(currentDocument.Header, requestedColumns, out int[] selectedIndexes, out string missingColumn))
                {
                    error.WriteLine($"Column not found: {missingColumn}");
                    return 1;
                }

                var distinctRows = new List<IReadOnlyList<string>>();
                var seenKeys = new HashSet<string[]>(new StringArrayComparer());

                foreach (IReadOnlyList<string> row in currentDocument.Rows)
                {
                    string[] key = BuildDistinctKey(row, selectedIndexes);
                    if (seenKeys.Add(key))
                    {
                        distinctRows.Add(ProjectRow(row, selectedIndexes));
                    }
                }

                currentDocument = new CsvDocument(
                    selectedIndexes.Select(index => currentDocument.Header[index]).ToArray(),
                    distinctRows,
                    currentDocument.LineEnding,
                    currentDocument.EndsWithLineEnding);
                continue;
            }

            if (string.Equals(command, "groupby", StringComparison.Ordinal))
            {
                if (argumentIndex >= args.Length)
                {
                    error.WriteLine(Usage);
                    return 1;
                }

                string groupColumnName = args[argumentIndex++];
                if (argumentIndex >= args.Length)
                {
                    error.WriteLine(Usage);
                    return 1;
                }

                string aggregateName = args[argumentIndex++];
                if (string.Equals(aggregateName, "count", StringComparison.Ordinal))
                {
                    if (!TryGroupDocument(
                            currentDocument,
                            groupColumnName,
                            GroupAggregateKind.Count,
                            null,
                            out CsvDocument groupedDocument,
                            out string groupError))
                    {
                        error.WriteLine(groupError);
                        return 1;
                    }

                    currentDocument = groupedDocument;
                    continue;
                }

                if (string.Equals(aggregateName, "sum", StringComparison.Ordinal))
                {
                    if (argumentIndex >= args.Length)
                    {
                        error.WriteLine(Usage);
                        return 1;
                    }

                    string aggregateColumnName = args[argumentIndex++];
                    if (!TryGroupDocument(
                            currentDocument,
                            groupColumnName,
                            GroupAggregateKind.Sum,
                            aggregateColumnName,
                            out CsvDocument groupedDocument,
                            out string groupError))
                    {
                        error.WriteLine(groupError);
                        return 1;
                    }

                    currentDocument = groupedDocument;
                    continue;
                }

                error.WriteLine("Invalid group aggregate.");
                return 1;
            }

            if (string.Equals(command, "count", StringComparison.Ordinal))
            {
                if (argumentIndex != args.Length)
                {
                    error.WriteLine(Usage);
                    return 1;
                }

                WriteScalarResult(output, currentDocument.Rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), currentDocument.LineEnding);
                return 0;
            }

            if (string.Equals(command, "sum", StringComparison.Ordinal))
            {
                if (argumentIndex >= args.Length || argumentIndex != args.Length - 1)
                {
                    error.WriteLine(Usage);
                    return 1;
                }

                string columnName = args[argumentIndex++];
                IReadOnlyDictionary<string, int> columnLookup = BuildColumnLookup(currentDocument.Header);
                if (!columnLookup.TryGetValue(columnName, out int columnIndex))
                {
                    error.WriteLine($"Column not found: {columnName}");
                    return 1;
                }

                decimal total = 0;
                foreach (IReadOnlyList<string> row in currentDocument.Rows)
                {
                    string value = columnIndex < row.Count ? row[columnIndex] : string.Empty;
                    if (!decimal.TryParse(
                            value,
                            System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out decimal numericValue))
                    {
                        error.WriteLine($"Column contains non-numeric values: {columnName}");
                        return 1;
                    }

                    total += numericValue;
                }

                WriteScalarResult(output, total.ToString(System.Globalization.CultureInfo.InvariantCulture), currentDocument.LineEnding);
                return 0;
            }

            error.WriteLine(Usage);
            return 1;
        }

        CsvDocument.WriteDocument(currentDocument, currentDocument.Rows, output);
        return 0;
    }

    private static bool TryBuildSelection(
        IReadOnlyList<string> header,
        IReadOnlyList<string> requestedColumns,
        out int[] selectedIndexes,
        out string missingColumn)
    {
        var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < header.Count; i++)
        {
            if (!lookup.ContainsKey(header[i]))
            {
                lookup[header[i]] = i;
            }
        }

        selectedIndexes = new int[requestedColumns.Count];
        for (int i = 0; i < requestedColumns.Count; i++)
        {
            string column = requestedColumns[i];
            if (!lookup.TryGetValue(column, out int index))
            {
                missingColumn = column;
                selectedIndexes = Array.Empty<int>();
                return false;
            }

            selectedIndexes[i] = index;
        }

        missingColumn = string.Empty;
        return true;
    }

    private static string[] ProjectRow(IReadOnlyList<string> row, IReadOnlyList<int> selectedIndexes)
    {
        var projectedRow = new string[selectedIndexes.Count];
        for (int i = 0; i < selectedIndexes.Count; i++)
        {
            int index = selectedIndexes[i];
            projectedRow[i] = index < row.Count ? row[index] : string.Empty;
        }

        return projectedRow;
    }

    private static IReadOnlyDictionary<string, int> BuildColumnLookup(IReadOnlyList<string> header)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < header.Count; i++)
        {
            if (!result.ContainsKey(header[i]))
            {
                result[header[i]] = i;
            }
        }

        return result;
    }

    private static bool TrySortDocument(
        CsvDocument document,
        string columnName,
        bool descending,
        out IReadOnlyList<IReadOnlyList<string>> sortedRows,
        out string error)
    {
        IReadOnlyDictionary<string, int> columnLookup = BuildColumnLookup(document.Header);
        if (!columnLookup.TryGetValue(columnName, out int columnIndex))
        {
            sortedRows = Array.Empty<IReadOnlyList<string>>();
            error = $"Column not found: {columnName}";
            return false;
        }

        bool sortAsNumeric = true;
        for (int i = 0; i < document.Rows.Count; i++)
        {
            string value = columnIndex < document.Rows[i].Count ? document.Rows[i][columnIndex] : string.Empty;
            if (!decimal.TryParse(
                    value,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _))
            {
                sortAsNumeric = false;
                break;
            }
        }

        IEnumerable<IReadOnlyList<string>> orderedRows;
        if (sortAsNumeric)
        {
            orderedRows = descending
                ? document.Rows.OrderByDescending(row => GetNumericSortValue(row, columnIndex))
                : document.Rows.OrderBy(row => GetNumericSortValue(row, columnIndex));
        }
        else
        {
            orderedRows = descending
                ? document.Rows.OrderByDescending(row => GetTextSortValue(row, columnIndex), StringComparer.Ordinal)
                : document.Rows.OrderBy(row => GetTextSortValue(row, columnIndex), StringComparer.Ordinal);
        }

        sortedRows = orderedRows.ToArray();
        error = string.Empty;
        return true;
    }

    private static bool TryGroupDocument(
        CsvDocument document,
        string groupColumnName,
        GroupAggregateKind aggregateKind,
        string? aggregateColumnName,
        out CsvDocument groupedDocument,
        out string error)
    {
        IReadOnlyDictionary<string, int> columnLookup = BuildColumnLookup(document.Header);
        if (!columnLookup.TryGetValue(groupColumnName, out int groupColumnIndex))
        {
            groupedDocument = null!;
            error = $"Column not found: {groupColumnName}";
            return false;
        }

        int aggregateColumnIndex = -1;
        if (aggregateKind == GroupAggregateKind.Sum)
        {
            if (aggregateColumnName is null || !columnLookup.TryGetValue(aggregateColumnName, out aggregateColumnIndex))
            {
                groupedDocument = null!;
                error = $"Column not found: {aggregateColumnName}";
                return false;
            }
        }

        var groups = new List<GroupState>();
        var groupLookup = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (IReadOnlyList<string> row in document.Rows)
        {
            string groupValue = GetTextSortValue(row, groupColumnIndex);
            if (!groupLookup.TryGetValue(groupValue, out int groupIndex))
            {
                groupIndex = groups.Count;
                groupLookup[groupValue] = groupIndex;
                groups.Add(new GroupState(groupValue));
            }

            GroupState group = groups[groupIndex];
            group.Count++;

            if (aggregateKind == GroupAggregateKind.Sum)
            {
                string value = GetTextSortValue(row, aggregateColumnIndex);
                if (!decimal.TryParse(
                        value,
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out decimal numericValue))
                {
                    groupedDocument = null!;
                    error = $"Column contains non-numeric values: {aggregateColumnName}";
                    return false;
                }

                group.Sum += numericValue;
            }
        }

        var groupedRows = new List<IReadOnlyList<string>>(groups.Count);
        string aggregateHeader = aggregateKind == GroupAggregateKind.Count ? "count" : "sum";

        foreach (GroupState group in groups)
        {
            groupedRows.Add(
                new[]
                {
                    group.Key,
                    aggregateKind == GroupAggregateKind.Count
                        ? group.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : group.Sum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
        }

        groupedDocument = new CsvDocument(
            new[] { groupColumnName, aggregateHeader },
            groupedRows,
            document.LineEnding,
            document.EndsWithLineEnding);
        error = string.Empty;
        return true;
    }

    private static string[] BuildDistinctKey(IReadOnlyList<string> row, IReadOnlyList<int> selectedIndexes)
    {
        var key = new string[selectedIndexes.Count];
        for (int i = 0; i < selectedIndexes.Count; i++)
        {
            int index = selectedIndexes[i];
            key[i] = index < row.Count ? row[index] : string.Empty;
        }

        return key;
    }

    private static bool TryParseSortDirection(string? directionArgument, out bool descending, out string error)
    {
        if (directionArgument is null)
        {
            descending = false;
            error = string.Empty;
            return true;
        }

        if (string.Equals(directionArgument, "asc", StringComparison.Ordinal))
        {
            descending = false;
            error = string.Empty;
            return true;
        }

        if (string.Equals(directionArgument, "desc", StringComparison.Ordinal))
        {
            descending = true;
            error = string.Empty;
            return true;
        }

        descending = false;
        error = "Invalid sort direction.";
        return false;
    }

    private static bool IsSortDirection(string value)
    {
        return string.Equals(value, "asc", StringComparison.Ordinal) ||
               string.Equals(value, "desc", StringComparison.Ordinal);
    }

    private static bool IsCommandToken(string value)
    {
        return string.Equals(value, "select", StringComparison.Ordinal) ||
               string.Equals(value, "where", StringComparison.Ordinal) ||
               string.Equals(value, "sort", StringComparison.Ordinal) ||
               string.Equals(value, "head", StringComparison.Ordinal) ||
               string.Equals(value, "distinct", StringComparison.Ordinal) ||
               string.Equals(value, "groupby", StringComparison.Ordinal) ||
               string.Equals(value, "count", StringComparison.Ordinal) ||
               string.Equals(value, "sum", StringComparison.Ordinal);
    }

    private static decimal GetNumericSortValue(IReadOnlyList<string> row, int columnIndex)
    {
        string value = GetTextSortValue(row, columnIndex);
        return decimal.Parse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GetTextSortValue(IReadOnlyList<string> row, int columnIndex)
    {
        return columnIndex < row.Count ? row[columnIndex] : string.Empty;
    }

    private static bool TryParseWhereExpression(
        string expression,
        out string columnName,
        out ComparisonOperator comparisonOperator,
        out string literalValue,
        out string error)
    {
        string[] operators =
        [
            ">=",
            "<=",
            "!=",
            "=",
            ">",
            "<",
        ];

        foreach (string operatorToken in operators)
        {
            int operatorIndex = expression.IndexOf(operatorToken, StringComparison.Ordinal);
            if (operatorIndex < 0)
            {
                continue;
            }

            columnName = expression[..operatorIndex].Trim();
            literalValue = expression[(operatorIndex + operatorToken.Length)..].Trim();

            if (columnName.Length == 0)
            {
                break;
            }

            comparisonOperator = operatorToken switch
            {
                "=" => ComparisonOperator.Equals,
                "!=" => ComparisonOperator.NotEquals,
                ">" => ComparisonOperator.GreaterThan,
                "<" => ComparisonOperator.LessThan,
                ">=" => ComparisonOperator.GreaterThanOrEqual,
                "<=" => ComparisonOperator.LessThanOrEqual,
                _ => throw new InvalidOperationException("Unknown comparison operator."),
            };

            error = string.Empty;
            return true;
        }

        columnName = string.Empty;
        comparisonOperator = ComparisonOperator.Equals;
        literalValue = string.Empty;
        error = "Invalid where expression.";
        return false;
    }

    private static void WriteScalarResult(TextWriter output, string value, string lineEnding)
    {
        output.Write(value);
        output.Write(lineEnding);
    }

    private static bool MatchesComparison(string leftValue, string rightValue, ComparisonOperator comparisonOperator)
    {
        if (decimal.TryParse(leftValue, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal leftNumber) &&
            decimal.TryParse(rightValue, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal rightNumber))
        {
            int numericComparison = decimal.Compare(leftNumber, rightNumber);
            return comparisonOperator switch
            {
                ComparisonOperator.Equals => numericComparison == 0,
                ComparisonOperator.NotEquals => numericComparison != 0,
                ComparisonOperator.GreaterThan => numericComparison > 0,
                ComparisonOperator.LessThan => numericComparison < 0,
                ComparisonOperator.GreaterThanOrEqual => numericComparison >= 0,
                ComparisonOperator.LessThanOrEqual => numericComparison <= 0,
                _ => throw new InvalidOperationException("Unknown comparison operator."),
            };
        }

        int textComparison = string.CompareOrdinal(leftValue, rightValue);
        return comparisonOperator switch
        {
            ComparisonOperator.Equals => textComparison == 0,
            ComparisonOperator.NotEquals => textComparison != 0,
            ComparisonOperator.GreaterThan => textComparison > 0,
            ComparisonOperator.LessThan => textComparison < 0,
            ComparisonOperator.GreaterThanOrEqual => textComparison >= 0,
            ComparisonOperator.LessThanOrEqual => textComparison <= 0,
            _ => throw new InvalidOperationException("Unknown comparison operator."),
        };
    }

    private enum ComparisonOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
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

            for (int i = 0; i < x.Length; i++)
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
            for (int i = 0; i < obj.Length; i++)
            {
                hashCode.Add(obj[i], StringComparer.Ordinal);
            }

            return hashCode.ToHashCode();
        }
    }

    private enum GroupAggregateKind
    {
        Count,
        Sum,
    }

    private sealed class GroupState
    {
        public GroupState(string key)
        {
            Key = key;
        }

        public string Key { get; }

        public int Count { get; set; }

        public decimal Sum { get; set; }
    }
}
