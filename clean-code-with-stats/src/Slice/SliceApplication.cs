namespace Slice;

public sealed class SliceApplication
{
    private const string UsageMessage =
        "Usage: slice <csv-file> select <columns> | where <expression> | sort <column> [asc|desc] | head <count> | distinct <column> [<column>...] | count | sum <column> | groupby <column> count | groupby <column> sum <column> [--format csv|json|table]";

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
        if (!TryParseArguments(args, out var inputPath, out var command, out var commandArguments, out var outputFormat))
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

        var outcome = command switch
        {
            "select" => ExecuteSelect(input, commandArguments),
            "where" => ExecuteWhere(input, commandArguments),
            "sort" => ExecuteSort(input, commandArguments),
            "head" => ExecuteHead(input, commandArguments),
            "distinct" => ExecuteDistinct(input, commandArguments),
            "count" => ExecuteCount(input),
            "sum" => ExecuteSum(input, commandArguments),
            "groupby" => ExecuteGroupBy(input, commandArguments),
            _ => ExecutionOutcome.Failure(UsageMessage)
        };

        if (outcome.ErrorMessage is not null)
        {
            await _error.WriteLineAsync(outcome.ErrorMessage).ConfigureAwait(false);
            return 1;
        }

        await OutputRenderer.RenderAsync(_output, outcome.Result!, outputFormat).ConfigureAwait(false);
        return 0;
    }

    private ExecutionOutcome ExecuteSelect(Stream input, IReadOnlyList<string> commandArguments)
    {
        IReadOnlyList<string> selectedColumns = _csvProcessor.ParseRequestedColumns(commandArguments);
        return _csvProcessor.SelectColumns(input, selectedColumns);
    }

    private ExecutionOutcome ExecuteWhere(Stream input, IReadOnlyList<string> commandArguments)
    {
        return _csvProcessor.FilterRows(input, commandArguments[0]);
    }

    private ExecutionOutcome ExecuteSort(Stream input, IReadOnlyList<string> commandArguments)
    {
        return _csvProcessor.SortRows(input, commandArguments[0], commandArguments.Count > 1 ? commandArguments[1] : null);
    }

    private ExecutionOutcome ExecuteHead(Stream input, IReadOnlyList<string> commandArguments)
    {
        return _csvProcessor.HeadRows(input, commandArguments[0]);
    }

    private ExecutionOutcome ExecuteDistinct(Stream input, IReadOnlyList<string> commandArguments)
    {
        IReadOnlyList<string> distinctColumns = _csvProcessor.ParseRequestedColumns(commandArguments);
        return _csvProcessor.DistinctRows(input, distinctColumns);
    }

    private ExecutionOutcome ExecuteCount(Stream input)
    {
        return _csvProcessor.CountRows(input);
    }

    private ExecutionOutcome ExecuteSum(Stream input, IReadOnlyList<string> commandArguments)
    {
        return _csvProcessor.SumRows(input, commandArguments[0]);
    }

    private ExecutionOutcome ExecuteGroupBy(Stream input, IReadOnlyList<string> commandArguments)
    {
        return _csvProcessor.GroupRows(input, commandArguments);
    }

    private static bool TryParseArguments(
        IReadOnlyList<string> args,
        out string inputPath,
        out string command,
        out IReadOnlyList<string> commandArguments,
        out OutputFormat outputFormat)
    {
        inputPath = string.Empty;
        command = string.Empty;
        commandArguments = Array.Empty<string>();
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

        command = remainingArguments[0];
        commandArguments = remainingArguments.Count > 1 ? remainingArguments.Skip(1).ToArray() : Array.Empty<string>();

        return command switch
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
}
