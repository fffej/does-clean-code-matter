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
        if (args.Length != 3 || !string.Equals(args[1], "select", StringComparison.Ordinal))
        {
            stderr.WriteLine("Usage: slice <csv-file> select <column1,column2,...>");
            return 1;
        }

        try
        {
            var selectedColumns = args[2]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            CsvRoundTripper.WriteSelectedColumns(args[0], selectedColumns, outputStream);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
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
}
