namespace Slice;

public sealed class SliceApplication
{
    private const string UsageMessage =
        "Usage: slice <csv-file> select <columns> | where <expression> | sort <column> [asc|desc] | head <count> | distinct <column> [<column>...] | count | sum <column> | groupby <column> count | groupby <column> sum <column> [--format csv|json|table]\n" +
        "Commands may be chained with | and are evaluated from left to right.";

    private readonly Stream _output;
    private readonly TextWriter _error;
    private readonly CsvTableProcessor _csvProcessor = new();

    public SliceApplication(Stream output, TextWriter error)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (!TryParseArguments(args, out var inputPath, out var commands, out var outputFormat))
        {
            await _error.WriteLineAsync(UsageMessage).ConfigureAwait(false);
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            await _error.WriteLineAsync($"File not found: {inputPath}").ConfigureAwait(false);
            return 1;
        }

        await using var input = File.OpenRead(inputPath);

        var loadOutcome = _csvProcessor.LoadTable(input);
        if (loadOutcome.ErrorMessage is not null)
        {
            await _error.WriteLineAsync(loadOutcome.ErrorMessage).ConfigureAwait(false);
            return 1;
        }

        QueryResult currentResult = loadOutcome.Result!;
        foreach (var command in commands)
        {
            var outcome = _csvProcessor.ApplyCommand(currentResult, command.Name, command.Arguments);
            if (outcome.ErrorMessage is not null)
            {
                await _error.WriteLineAsync(outcome.ErrorMessage).ConfigureAwait(false);
                return 1;
            }

            currentResult = outcome.Result!;
        }

        await OutputRenderer.RenderAsync(_output, currentResult, outputFormat).ConfigureAwait(false);
        return 0;
    }

    private static bool TryParseArguments(
        IReadOnlyList<string> args,
        out string inputPath,
        out IReadOnlyList<PipelineCommand> commands,
        out OutputFormat outputFormat)
    {
        inputPath = string.Empty;
        commands = Array.Empty<PipelineCommand>();
        outputFormat = OutputFormat.Csv;

        if (args.Count < 2)
        {
            return false;
        }

        inputPath = args[0];

        var remainingArguments = new List<string>();
        var formatSpecified = false;

        for (var i = 1; i < args.Count; i++)
        {
            var current = args[i];
            if (string.Equals(current, "--format", StringComparison.OrdinalIgnoreCase))
            {
                if (formatSpecified || i + 1 >= args.Count || !TryParseOutputFormat(args[i + 1], out outputFormat))
                {
                    return false;
                }

                formatSpecified = true;
                i++;
                continue;
            }

            remainingArguments.Add(current);
        }

        if (remainingArguments.Count == 0)
        {
            return false;
        }

        var pipelineTokens = remainingArguments.ToArray();
        if (pipelineTokens.Length == 0)
        {
            return false;
        }

        if (!TryParsePipelineCommands(pipelineTokens, out commands))
        {
            return false;
        }

        return commands.Count > 0;
    }

    private static bool TryParseOutputFormat(string value, out OutputFormat outputFormat)
    {
        if (string.Equals(value, "csv", StringComparison.OrdinalIgnoreCase))
        {
            outputFormat = OutputFormat.Csv;
            return true;
        }

        if (string.Equals(value, "json", StringComparison.OrdinalIgnoreCase))
        {
            outputFormat = OutputFormat.Json;
            return true;
        }

        if (string.Equals(value, "table", StringComparison.OrdinalIgnoreCase))
        {
            outputFormat = OutputFormat.Table;
            return true;
        }

        outputFormat = default;
        return false;
    }

    private static bool TryParsePipelineCommands(
        IReadOnlyList<string> pipelineTokens,
        out IReadOnlyList<PipelineCommand> commands)
    {
        var parsedCommands = new List<PipelineCommand>();
        var currentTokens = new List<string>();

        foreach (var token in pipelineTokens)
        {
            if (string.Equals(token, "|", StringComparison.Ordinal))
            {
                if (!TryAddParsedCommand(currentTokens, parsedCommands))
                {
                    commands = Array.Empty<PipelineCommand>();
                    return false;
                }

                currentTokens.Clear();
                continue;
            }

            currentTokens.Add(token);
        }

        if (!TryAddParsedCommand(currentTokens, parsedCommands))
        {
            commands = Array.Empty<PipelineCommand>();
            return false;
        }

        commands = parsedCommands;
        return commands.Count > 0;
    }

    private static bool TryAddParsedCommand(
        IReadOnlyList<string> commandTokens,
        ICollection<PipelineCommand> commands)
    {
        if (commandTokens.Count == 0)
        {
            return false;
        }

        var commandName = commandTokens[0];
        var commandArguments = commandTokens.Count > 1
            ? commandTokens.Skip(1).ToArray()
            : Array.Empty<string>();

        if (!IsValidCommandShape(commandName, commandArguments))
        {
            return false;
        }

        commands.Add(new PipelineCommand(commandName, commandArguments));
        return true;
    }

    private static bool IsValidCommandShape(string commandName, IReadOnlyList<string> commandArguments)
    {
        return commandName switch
        {
            "select" or "where" or "head" => commandArguments.Count == 1,
            "sort" => commandArguments.Count is 1 or 2,
            "distinct" => commandArguments.Count >= 1,
            "count" => commandArguments.Count == 0,
            "sum" => commandArguments.Count == 1,
            "groupby" => commandArguments.Count is 2 or 3,
            _ => false
        };
    }

    private sealed record PipelineCommand(string Name, IReadOnlyList<string> Arguments);
}
