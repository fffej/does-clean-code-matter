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
        if (args.Count != 3)
        {
            await _error.WriteLineAsync("Usage: slice <csv-file> select <columns> | where <expression>");
            return 1;
        }

        var inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            await _error.WriteLineAsync($"File not found: {inputPath}");
            return 1;
        }

        await using var input = File.OpenRead(inputPath);
        var command = args[1];
        var commandArgument = args[2];

        string? result = command switch
        {
            "select" => await ExecuteSelectAsync(input, commandArgument).ConfigureAwait(false),
            "where" => await _csvProcessor.WriteFilteredRowsAsync(input, _output, commandArgument).ConfigureAwait(false),
            _ => "Usage: slice <csv-file> select <columns> | where <expression>"
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

}
