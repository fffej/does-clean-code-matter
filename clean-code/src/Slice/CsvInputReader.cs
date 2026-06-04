namespace Slice;

internal static class CsvInputReader
{
    public static bool TryReadRows(string csvPath, TextWriter error, out List<IReadOnlyList<string>> rows)
    {
        rows = [];

        if (!File.Exists(csvPath))
        {
            error.WriteLine($"Input file not found: {csvPath}");
            return false;
        }

        try
        {
            using StreamReader reader = File.OpenText(csvPath);
            rows = CsvParser.Parse(reader);
        }
        catch (FormatException exception)
        {
            error.WriteLine(exception.Message);
            return false;
        }

        if (rows.Count == 0)
        {
            error.WriteLine("Input file is empty.");
            return false;
        }

        return true;
    }
}
