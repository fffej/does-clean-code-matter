namespace Slice;

public static class Program
{
    public static int Main(string[] args)
    {
        return CsvPassthroughApp.Run(args, Console.OpenStandardOutput(), Console.Error);
    }
}
