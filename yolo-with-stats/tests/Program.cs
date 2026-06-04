using System.Text;
using Slice;

var tests = new List<(string Name, Action Test)>
{
    ("selects named columns in requested order", SelectsNamedColumnsInRequestedOrder),
    ("returns failure when a selected column is missing", ReturnsFailureWhenSelectedColumnIsMissing),
    ("returns failure when arguments are invalid", ReturnsFailureWhenArgumentsAreInvalid),
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

static void SelectsNamedColumnsInRequestedOrder()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    var originalBytes = Encoding.UTF8.GetBytes("name,age,city\r\nAda,36,London\r\nGrace,47,Paris\r\n");
    File.WriteAllBytes(tempPath, originalBytes);

    try
    {
        using var output = new MemoryStream();
        CsvRoundTripper.WriteSelectedColumns(tempPath, new[] { "name", "age" }, output);

        var result = Encoding.UTF8.GetString(output.ToArray());
        var expected = $"name,age{Environment.NewLine}Ada,36{Environment.NewLine}Grace,47{Environment.NewLine}";
        if (result != expected)
        {
            throw new Exception($"unexpected output: {result}");
        }
    }
    finally
    {
        File.Delete(tempPath);
    }
}

static void ReturnsFailureWhenSelectedColumnIsMissing()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,age\r\nAda,36\r\n");

    try
    {
        using var output = new MemoryStream();
        using var stderr = new StringWriter();
        var exitCode = SliceApp.Run([tempPath, "select", "name,city"], stderr, output);
        if (exitCode == 0)
        {
            throw new Exception("expected non-zero exit code");
        }

        if (!stderr.ToString().Contains("Column not found: city", StringComparison.Ordinal))
        {
            throw new Exception($"unexpected error output: {stderr}");
        }
    }
    finally
    {
        File.Delete(tempPath);
    }
}

static void ReturnsFailureWhenArgumentsAreInvalid()
{
    using var output = new MemoryStream();
    using var stderr = new StringWriter();
    var exitCode = SliceApp.Run(Array.Empty<string>(), stderr, output);
    if (exitCode == 0)
    {
        throw new Exception("expected non-zero exit code");
    }
}
