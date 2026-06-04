using System.Globalization;
using System.Text;

namespace Slice;

public static class CsvSortApp
{
    public static int Run(string csvPath, string sortColumn, string sortDirection, Stream output, TextWriter error)
    {
        if (!TryParseSortArgument(sortColumn, sortDirection, out SortSpecification specification))
        {
            error.WriteLine($"Invalid sort expression: {sortColumn} {sortDirection}".TrimEnd());
            return 1;
        }

        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return 1;
        }

        IReadOnlyList<string> header = rows[0];
        int columnIndex = CsvHeaderLookup.FindHeaderIndex(header, specification.ColumnName);
        if (columnIndex < 0)
        {
            error.WriteLine($"Column not found: {specification.ColumnName}");
            return 1;
        }

        List<IReadOnlyList<string>> dataRows = rows.Skip(1).ToList();
        bool sortAsNumbers = CanSortAsNumbers(dataRows, columnIndex);

        IEnumerable<IReadOnlyList<string>> sortedRows = sortAsNumbers
            ? SortRowsNumerically(dataRows, columnIndex, specification.Direction)
            : SortRowsAsText(dataRows, columnIndex, specification.Direction);

        using StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(CsvWriter.FormatRow(header));
        writer.Write("\r\n");

        foreach (IReadOnlyList<string> row in sortedRows)
        {
            writer.Write(CsvWriter.FormatRow(row));
            writer.Write("\r\n");
        }

        writer.Flush();
        output.Flush();
        return 0;
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
        IEnumerable<IReadOnlyList<string>> rows,
        int columnIndex,
        SortDirection direction)
    {
        IOrderedEnumerable<IReadOnlyList<string>> sorted = direction == SortDirection.Ascending
            ? rows.OrderBy(row => decimal.Parse(GetColumnValue(row, columnIndex), CultureInfo.InvariantCulture))
            : rows.OrderByDescending(row => decimal.Parse(GetColumnValue(row, columnIndex), CultureInfo.InvariantCulture));

        return sorted;
    }

    private static IEnumerable<IReadOnlyList<string>> SortRowsAsText(
        IEnumerable<IReadOnlyList<string>> rows,
        int columnIndex,
        SortDirection direction)
    {
        IOrderedEnumerable<IReadOnlyList<string>> sorted = direction == SortDirection.Ascending
            ? rows.OrderBy(row => GetColumnValue(row, columnIndex), StringComparer.Ordinal)
            : rows.OrderByDescending(row => GetColumnValue(row, columnIndex), StringComparer.Ordinal);

        return sorted;
    }

    private static string GetColumnValue(IReadOnlyList<string> row, int columnIndex)
    {
        return columnIndex < row.Count ? row[columnIndex] : string.Empty;
    }

    private readonly record struct SortSpecification(string ColumnName, SortDirection Direction);

    private enum SortDirection
    {
        Ascending,
        Descending
    }
}
