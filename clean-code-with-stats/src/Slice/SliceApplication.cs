namespace Slice;

public sealed class SliceApplication
{
    private readonly Stream _output;
    private readonly TextWriter _error;
    private readonly CsvColumnSelector _columnSelector = new();

    public SliceApplication(Stream output, TextWriter error)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (args.Count != 3 || !string.Equals(args[1], "select", StringComparison.Ordinal))
        {
            await _error.WriteLineAsync("Usage: slice <csv-file> select <columns>");
            return 1;
        }

        var inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            await _error.WriteLineAsync($"File not found: {inputPath}");
            return 1;
        }

        IReadOnlyList<string> selectedColumns = _columnSelector.ParseRequestedColumns(args[2]);

        await using var input = File.OpenRead(inputPath);
        var selectionResult = await _columnSelector.WriteSelectedColumnsAsync(input, _output, selectedColumns)
            .ConfigureAwait(false);

        if (selectionResult is not null)
        {
            await _error.WriteLineAsync(selectionResult);
            return 1;
        }

        return 0;
    }
}
