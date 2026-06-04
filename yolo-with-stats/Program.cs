namespace Slice;

internal static class Program
{
    public static int Main(string[] args)
    {
        return SliceApp.Run(args, Console.Error, Console.OpenStandardOutput());
    }
}

public static class SliceApp
{
    public static int Run(string[] args, TextWriter stderr, Stream outputStream)
    {
        if (args.Length != 1)
        {
            stderr.WriteLine("Usage: slice <csv-file>");
            return 1;
        }

        try
        {
            CsvRoundTripper.CopyFileToStandardOutput(args[0], outputStream);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }
}

public static class CsvRoundTripper
{
    public static void CopyFileToStandardOutput(string path, Stream output)
    {
        using var input = File.OpenRead(path);
        input.CopyTo(output);
        output.Flush();
    }
}
