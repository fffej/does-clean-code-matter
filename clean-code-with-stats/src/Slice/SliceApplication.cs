namespace Slice;

public sealed class SliceApplication
{
    private readonly Stream _output;
    private readonly TextWriter _error;

    public SliceApplication(Stream output, TextWriter error)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (args.Count != 1)
        {
            await _error.WriteLineAsync("Usage: slice <csv-file>");
            return 1;
        }

        var inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            await _error.WriteLineAsync($"File not found: {inputPath}");
            return 1;
        }

        await using var input = File.OpenRead(inputPath);
        await input.CopyToAsync(_output).ConfigureAwait(false);
        await _output.FlushAsync().ConfigureAwait(false);
        return 0;
    }
}
