namespace Slice;

public static class CsvPassthroughApp
{
    public static int Run(string[] args, Stream output, TextWriter error)
    {
        if (args.Length == 1)
        {
            return RunPassthrough(args[0], output, error);
        }

        if (args.Length == 3 && string.Equals(args[1], "select", StringComparison.Ordinal))
        {
            return CsvSelectApp.Run(args[0], args[2], output, error);
        }

        if (args.Length == 3 && string.Equals(args[1], "where", StringComparison.Ordinal))
        {
            return CsvWhereApp.Run(args[0], args[2], output, error);
        }

        if (args.Length == 3 && string.Equals(args[1], "head", StringComparison.Ordinal))
        {
            return CsvHeadApp.Run(args[0], args[2], output, error);
        }

        if ((args.Length == 3 || args.Length == 4) && string.Equals(args[1], "sort", StringComparison.Ordinal))
        {
            string sortDirection = args.Length == 4 ? args[3] : string.Empty;
            return CsvSortApp.Run(args[0], args[2], sortDirection, output, error);
        }

        error.WriteLine("Usage: slice <csv-file>");
        error.WriteLine("Usage: slice <csv-file> select <column1,column2,...>");
        error.WriteLine("Usage: slice <csv-file> where <column><operator><value>");
        error.WriteLine("Usage: slice <csv-file> head <n>");
        error.WriteLine("Usage: slice <csv-file> sort <column> [asc|desc]");
        return 1;
    }

    private static int RunPassthrough(string csvPath, Stream output, TextWriter error)
    {
        if (!File.Exists(csvPath))
        {
            error.WriteLine($"Input file not found: {csvPath}");
            return 1;
        }

        using FileStream inputStream = File.OpenRead(csvPath);
        inputStream.CopyTo(output);
        output.Flush();
        return 0;
    }
}
