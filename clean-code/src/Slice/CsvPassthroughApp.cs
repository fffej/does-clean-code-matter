namespace Slice;

public static class CsvPassthroughApp
{
    public static int Run(string[] args, Stream output, TextWriter error)
    {
        if (!TryParseArguments(args, error, out string csvPath, out OutputFormat format, out string[] commandArgs))
        {
            return 1;
        }

        QueryResult? result;
        if (commandArgs.Length == 0)
        {
            if (format == OutputFormat.Csv)
            {
                return CopyPassthrough(csvPath, output, error);
            }

            result = RunPassthrough(csvPath, error);
        }
        else if (commandArgs.Length == 1 && string.Equals(commandArgs[0], "count", StringComparison.Ordinal))
        {
            result = CsvAggregateApp.RunCount(csvPath, error);
        }
        else if (commandArgs.Length == 2 && string.Equals(commandArgs[0], "select", StringComparison.Ordinal))
        {
            result = CsvSelectApp.Run(csvPath, commandArgs[1], error);
        }
        else if (commandArgs.Length == 2 && string.Equals(commandArgs[0], "where", StringComparison.Ordinal))
        {
            result = CsvWhereApp.Run(csvPath, commandArgs[1], error);
        }
        else if (commandArgs.Length == 2 && string.Equals(commandArgs[0], "head", StringComparison.Ordinal))
        {
            result = CsvHeadApp.Run(csvPath, commandArgs[1], error);
        }
        else if (commandArgs.Length >= 3 && string.Equals(commandArgs[0], "groupby", StringComparison.Ordinal))
        {
            result = CsvGroupByApp.Run(csvPath, commandArgs[1], commandArgs[2..], error);
        }
        else if (commandArgs.Length >= 2 && string.Equals(commandArgs[0], "distinct", StringComparison.Ordinal))
        {
            result = CsvDistinctApp.Run(csvPath, commandArgs[1..], error);
        }
        else if ((commandArgs.Length == 2 || commandArgs.Length == 3) && string.Equals(commandArgs[0], "sort", StringComparison.Ordinal))
        {
            string sortDirection = commandArgs.Length == 3 ? commandArgs[2] : string.Empty;
            result = CsvSortApp.Run(csvPath, commandArgs[1], sortDirection, error);
        }
        else if (commandArgs.Length == 2 && string.Equals(commandArgs[0], "sum", StringComparison.Ordinal))
        {
            result = CsvAggregateApp.RunSum(csvPath, commandArgs[1], error);
        }
        else
        {
            WriteUsage(error);
            return 1;
        }

        if (result is null)
        {
            return 1;
        }

        QueryResultRenderer.Write(result, format, output);
        return 0;
    }

    private static bool TryParseArguments(
        string[] args,
        TextWriter error,
        out string csvPath,
        out OutputFormat format,
        out string[] commandArgs)
    {
        csvPath = string.Empty;
        format = OutputFormat.Csv;
        commandArgs = [];

        if (args.Length == 0)
        {
            WriteUsage(error);
            return false;
        }

        csvPath = args[0];
        List<string> filteredArgs = [];
        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--format", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length)
                {
                    error.WriteLine("Missing value for --format.");
                    return false;
                }

                string formatArgument = args[++index];
                if (!OutputFormatParser.TryParse(formatArgument, out format))
                {
                    error.WriteLine($"Invalid format: {formatArgument}");
                    return false;
                }

                continue;
            }

            filteredArgs.Add(argument);
        }

        commandArgs = filteredArgs.ToArray();
        return true;
    }

    private static void WriteUsage(TextWriter error)
    {
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table]");
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table] count");
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table] sum <column>");
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table] select <column1,column2,...>");
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table] where <column><operator><value>");
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table] head <n>");
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table] groupby <column> count");
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table] groupby <column> sum <column>");
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table] distinct <column1> [<column2> ...]");
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table] sort <column> [asc|desc]");
    }

    private static int CopyPassthrough(string csvPath, Stream output, TextWriter error)
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

    private static QueryResult? RunPassthrough(string csvPath, TextWriter error)
    {
        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return null;
        }

        return new QueryResult.Table(rows[0], rows[1..]);
    }
}
