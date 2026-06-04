namespace Slice;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new SliceApplication(Console.OpenStandardOutput(), Console.Error);
        return await app.RunAsync(args);
    }
}
