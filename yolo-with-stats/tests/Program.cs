using System.Text;
using Slice;

var tests = new List<(string Name, Action Test)>
{
    ("copies csv bytes unchanged", CopiesCsvBytesUnchanged),
    ("returns failure when missing file argument", ReturnsFailureWhenMissingFileArgument),
};

var failures = 0;

foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

return failures == 0 ? 0 : 1;

static void CopiesCsvBytesUnchanged()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    var originalBytes = Encoding.UTF8.GetBytes("name,age\r\nAda,36\r\nGrace,47\r\n");
    File.WriteAllBytes(tempPath, originalBytes);

    try
    {
        using var output = new MemoryStream();
        CsvRoundTripper.CopyFileToStandardOutput(tempPath, output);

        var copiedBytes = output.ToArray();
        if (!originalBytes.SequenceEqual(copiedBytes))
        {
            throw new Exception("output bytes differed from input bytes");
        }
    }
    finally
    {
        File.Delete(tempPath);
    }
}

static void ReturnsFailureWhenMissingFileArgument()
{
    using var output = new MemoryStream();
    using var stderr = new StringWriter();
    var exitCode = SliceApp.Run(Array.Empty<string>(), stderr, output);
    if (exitCode == 0)
    {
        throw new Exception("expected non-zero exit code");
    }
}
