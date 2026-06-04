namespace Slice;

public static class App
{
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        if (args.Length != 1)
        {
            error.WriteLine("Usage: slice <csv-file>");
            return 1;
        }

        string path = args[0];

        if (!File.Exists(path))
        {
            error.WriteLine($"File not found: {path}");
            return 1;
        }

        string csv = File.ReadAllText(path);
        output.Write(csv);
        return 0;
    }
}
