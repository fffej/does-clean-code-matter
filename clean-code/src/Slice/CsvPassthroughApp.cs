namespace Slice;

public static class CsvPassthroughApp
{
    public static int Run(string[] args, Stream output, TextWriter error)
    {
        if (!TryParseArguments(args, error, out string csvPath, out OutputFormat format, out string[] commandArgs))
        {
            return 1;
        }

        if (!TryParsePipeline(commandArgs, error, out IReadOnlyList<PipelineCommand> commands))
        {
            return 1;
        }

        if (commands.Count == 0 && format == OutputFormat.Csv)
        {
            return CopyPassthrough(csvPath, output, error);
        }

        if (!CsvQueryOperations.TryReadInitialTable(csvPath, error, out QueryResult.Table table))
        {
            return 1;
        }

        QueryResult current = table;
        foreach (PipelineCommand command in commands)
        {
            if (!CsvQueryOperations.TryApply(current, command.Name, command.Arguments, error, out QueryResult next))
            {
                return 1;
            }

            current = next;
        }

        QueryResultRenderer.Write(current, format, output);
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

    private static bool TryParsePipeline(
        IReadOnlyList<string> commandArgs,
        TextWriter error,
        out IReadOnlyList<PipelineCommand> commands)
    {
        List<PipelineCommand> parsedCommands = [];
        List<string> currentCommandArguments = [];

        foreach (string argument in commandArgs)
        {
            if (string.Equals(argument, "|", StringComparison.Ordinal))
            {
                if (currentCommandArguments.Count == 0)
                {
                    error.WriteLine("Invalid pipeline expression.");
                    commands = [];
                    return false;
                }

                if (!TryCreatePipelineCommand(currentCommandArguments, error, out PipelineCommand command))
                {
                    commands = [];
                    return false;
                }

                parsedCommands.Add(command);
                currentCommandArguments = [];
                continue;
            }

            currentCommandArguments.Add(argument);
        }

        if (currentCommandArguments.Count > 0)
        {
            if (!TryCreatePipelineCommand(currentCommandArguments, error, out PipelineCommand command))
            {
                commands = [];
                return false;
            }

            parsedCommands.Add(command);
        }
        else if (commandArgs.Count > 0)
        {
            error.WriteLine("Invalid pipeline expression.");
            commands = [];
            return false;
        }

        commands = parsedCommands;
        return true;
    }

    private static bool TryCreatePipelineCommand(
        IReadOnlyList<string> commandArguments,
        TextWriter error,
        out PipelineCommand command)
    {
        if (commandArguments.Count == 0)
        {
            error.WriteLine("Invalid pipeline expression.");
            command = default;
            return false;
        }

        string commandName = commandArguments[0];
        string[] arguments = commandArguments.Count > 1 ? commandArguments.Skip(1).ToArray() : [];

        command = new PipelineCommand(commandName, arguments);
        return true;
    }

    private static void WriteUsage(TextWriter error)
    {
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table]");
        error.WriteLine("Usage: slice <csv-file> [--format csv|json|table] <command> [args...] [| <command> [args...]]...");
        error.WriteLine("Commands: count");
        error.WriteLine("Commands: sum <column>");
        error.WriteLine("Commands: select <column1,column2,...>");
        error.WriteLine("Commands: where <column><operator><value>");
        error.WriteLine("Commands: head <n>");
        error.WriteLine("Commands: groupby <column> count");
        error.WriteLine("Commands: groupby <column> sum <column>");
        error.WriteLine("Commands: distinct <column1> [<column2> ...]");
        error.WriteLine("Commands: sort <column> [asc|desc]");
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

    private readonly record struct PipelineCommand(string Name, string[] Arguments);
}
