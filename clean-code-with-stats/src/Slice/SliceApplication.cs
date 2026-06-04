namespace Slice;

public sealed class SliceApplication
{
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
        if (!TryParseArguments(args, out var inputPath, out var command, out var commandArgument, out var secondaryArgument))
        {
            await _error.WriteLineAsync("Usage: slice <csv-file> select <columns> | where <expression> | sort <column> [asc|desc] | head <count>");
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            await _error.WriteLineAsync($"File not found: {inputPath}");
            return 1;
        }

        await using var input = File.OpenRead(inputPath);

        string? result = command switch
        {
            "select" => await ExecuteSelectAsync(input, commandArgument).ConfigureAwait(false),
            "where" => await _csvProcessor.WriteFilteredRowsAsync(input, _output, commandArgument).ConfigureAwait(false),
            "sort" => await _csvProcessor.WriteSortedRowsAsync(input, _output, commandArgument, secondaryArgument).ConfigureAwait(false),
            "head" => await _csvProcessor.WriteHeadRowsAsync(input, _output, commandArgument).ConfigureAwait(false),
            _ => "Usage: slice <csv-file> select <columns> | where <expression> | sort <column> [asc|desc] | head <count>"
        };

        if (result is null)
        {
            return 0;
        }

        await _error.WriteLineAsync(result);
        return 1;
    }

    private Task<string?> ExecuteSelectAsync(Stream input, string columnsArgument)
    {
        IReadOnlyList<string> selectedColumns = _csvProcessor.ParseRequestedColumns(columnsArgument);
        return _csvProcessor.WriteSelectedColumnsAsync(input, _output, selectedColumns);
    }

    private static bool TryParseArguments(
        IReadOnlyList<string> args,
        out string inputPath,
        out string command,
        out string commandArgument,
        out string? secondaryArgument)
    {
        inputPath = string.Empty;
        command = string.Empty;
        commandArgument = string.Empty;
        secondaryArgument = null;

        if (args.Count < 3 || args.Count > 4)
        {
            return false;
        }

        inputPath = args[0];
        command = args[1];
        commandArgument = args[2];

        if (args.Count == 4)
        {
            secondaryArgument = args[3];
            if (!string.Equals(command, "sort", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return command switch
        {
            "select" or "where" or "head" => args.Count == 3,
            "sort" => args.Count is 3 or 4,
            _ => false
        };
    }

}
