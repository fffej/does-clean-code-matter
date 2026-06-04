namespace Slice;

public static class App
{
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        if (args.Length != 3 || !string.Equals(args[1], "select", StringComparison.Ordinal))
        {
            error.WriteLine("Usage: slice <csv-file> select <columns>");
            return 1;
        }

        string path = args[0];
        string columnSpec = args[2];

        if (!File.Exists(path))
        {
            error.WriteLine($"File not found: {path}");
            return 1;
        }

        string[] requestedColumns = columnSpec
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (requestedColumns.Length == 0)
        {
            error.WriteLine("No columns selected.");
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

        if (!TryBuildSelection(document.Header, requestedColumns, out int[] selectedIndexes, out string missingColumn))
        {
            error.WriteLine($"Column not found: {missingColumn}");
            return 1;
        }

        CsvDocument.WriteSelection(document, selectedIndexes, output);
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
}
