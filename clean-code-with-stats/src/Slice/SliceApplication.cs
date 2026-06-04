namespace Slice;

public sealed class SliceApplication
{
    private const string UsageMessage = "Usage: slice <csv-file> select <columns> | where <expression> | sort <column> [asc|desc] | head <count> | distinct <column> [<column>...] | count | sum <column>";

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
        if (!TryParseArguments(args, out var inputPath, out var command, out var commandArguments))
        {
            await _error.WriteLineAsync(UsageMessage);
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
            "select" => await ExecuteSelectAsync(input, commandArguments[0]).ConfigureAwait(false),
            "where" => await _csvProcessor.WriteFilteredRowsAsync(input, _output, commandArguments[0]).ConfigureAwait(false),
            "sort" => await _csvProcessor.WriteSortedRowsAsync(input, _output, commandArguments[0], commandArguments.Count > 1 ? commandArguments[1] : null).ConfigureAwait(false),
            "head" => await _csvProcessor.WriteHeadRowsAsync(input, _output, commandArguments[0]).ConfigureAwait(false),
            "distinct" => await ExecuteDistinctAsync(input, commandArguments).ConfigureAwait(false),
            "count" => await _csvProcessor.WriteCountAsync(input, _output).ConfigureAwait(false),
            "sum" => await _csvProcessor.WriteSumAsync(input, _output, commandArguments[0]).ConfigureAwait(false),
            _ => UsageMessage
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

    private Task<string?> ExecuteDistinctAsync(Stream input, IReadOnlyList<string> columnArguments)
    {
        IReadOnlyList<string> distinctColumns = _csvProcessor.ParseRequestedColumns(columnArguments);
        return _csvProcessor.WriteDistinctRowsAsync(input, _output, distinctColumns);
    }

    private static bool TryParseArguments(
        IReadOnlyList<string> args,
        out string inputPath,
        out string command,
        out IReadOnlyList<string> commandArguments)
    {
        inputPath = string.Empty;
        command = string.Empty;
        commandArguments = Array.Empty<string>();

        if (args.Count < 2)
        {
            return false;
        }

        inputPath = args[0];
        command = args[1];
        commandArguments = args.Count > 2 ? args.Skip(2).ToArray() : Array.Empty<string>();

        return command switch
        {
            "select" or "where" or "head" => commandArguments.Count == 1,
            "sort" => commandArguments.Count is 1 or 2,
            "distinct" => commandArguments.Count >= 1,
            "count" => commandArguments.Count == 0,
            "sum" => commandArguments.Count == 1,
            _ => false
        };
    }
}
