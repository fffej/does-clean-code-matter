namespace Slice;

public static class CsvPassthroughApp
{
    public static int Run(string[] args, Stream output, TextWriter error)
    {
        if (args.Length != 1)
        {
            error.WriteLine("Usage: slice <csv-file>");
            return 1;
        }

        string csvPath = args[0];
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
