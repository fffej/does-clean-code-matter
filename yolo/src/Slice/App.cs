namespace Slice;

public static class App
{
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        _ = input;

        if (args.Length < 3 || args.Length > 4)
        {
            error.WriteLine("Usage: slice <csv-file> select <columns> | where <expression> | sort <column> [asc|desc]");
            return 1;
        }

        string path = args[0];
        string command = args[1];
        string argument = args[2];
        string? optionalArgument = args.Length == 4 ? args[3] : null;

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

        if (string.Equals(command, "select", StringComparison.Ordinal))
        {
            string[] requestedColumns = argument
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (requestedColumns.Length == 0)
            {
                error.WriteLine("No columns selected.");
                return 1;
            }

            if (!TryBuildSelection(document.Header, requestedColumns, out int[] selectedIndexes, out string missingColumn))
            {
                error.WriteLine($"Column not found: {missingColumn}");
                return 1;
            }

            CsvDocument.WriteSelection(document, selectedIndexes, output);
            return 0;
        }

        if (string.Equals(command, "where", StringComparison.Ordinal))
        {
            if (!TryParseWhereExpression(argument, out string columnName, out ComparisonOperator comparisonOperator, out string literalValue, out string whereError))
            {
                error.WriteLine(whereError);
                return 1;
            }

            IReadOnlyDictionary<string, int> columnLookup = BuildColumnLookup(document.Header);
            if (!columnLookup.TryGetValue(columnName, out int columnIndex))
            {
                error.WriteLine($"Column not found: {columnName}");
                return 1;
            }

            var filteredRows = new List<IReadOnlyList<string>>();
            foreach (IReadOnlyList<string> row in document.Rows)
            {
                string leftValue = columnIndex < row.Count ? row[columnIndex] : string.Empty;
                if (MatchesComparison(leftValue, literalValue, comparisonOperator))
                {
                    filteredRows.Add(row);
                }
            }

            CsvDocument.WriteDocument(document, filteredRows, output);
            return 0;
        }

        if (string.Equals(command, "sort", StringComparison.Ordinal))
        {
            if (!TryParseSortDirection(optionalArgument, out bool descending, out string sortError))
            {
                error.WriteLine(sortError);
                return 1;
            }

            if (!TrySortDocument(document, argument, descending, out IReadOnlyList<IReadOnlyList<string>> sortedRows, out string sortColumnError))
            {
                error.WriteLine(sortColumnError);
                return 1;
            }

            CsvDocument.WriteDocument(document, sortedRows, output);
            return 0;
        }

        error.WriteLine("Usage: slice <csv-file> select <columns> | where <expression> | sort <column> [asc|desc]");
        return 1;
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
}
