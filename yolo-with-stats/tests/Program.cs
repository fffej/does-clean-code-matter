using System.Text;
using Slice;

var tests = new List<(string Name, Action Test)>
{
    ("selects named columns in requested order", SelectsNamedColumnsInRequestedOrder),
    ("returns failure when a selected column is missing", ReturnsFailureWhenSelectedColumnIsMissing),
    ("returns failure when arguments are invalid", ReturnsFailureWhenArgumentsAreInvalid),
    ("filters numeric rows with where", FiltersNumericRowsWithWhere),
    ("filters numeric rows with not-equal where", FiltersNumericRowsWithNotEqualWhere),
    ("filters text rows with where", FiltersTextRowsWithWhere),
    ("returns failure when a where column is missing", ReturnsFailureWhenWhereColumnIsMissing),
    ("returns failure when a where expression is invalid", ReturnsFailureWhenWhereExpressionIsInvalid),
    ("keeps only the first five data rows with head", KeepsOnlyTheFirstFiveDataRowsWithHead),
    ("keeps all rows when head exceeds row count", KeepsAllRowsWhenHeadExceedsRowCount),
    ("returns failure when head row count is not positive", ReturnsFailureWhenHeadRowCountIsNotPositive),
    ("sorts numeric rows descending", SortsNumericRowsDescending),
    ("sorts rows ascending by default", SortsRowsAscendingByDefault),
    ("sorts text rows descending", SortsTextRowsDescending),
    ("returns failure when a sort column is missing", ReturnsFailureWhenSortColumnIsMissing),
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

static void FiltersNumericRowsWithWhere()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,age,city\r\nAda,36,London\r\nGrace,29,Paris\r\nKatherine,41,New York\r\n");

    try
    {
        using var output = new MemoryStream();
        CsvRoundTripper.WriteFilteredRows(tempPath, "age>30", output);

        var result = Encoding.UTF8.GetString(output.ToArray());
        var expected = $"name,age,city{Environment.NewLine}Ada,36,London{Environment.NewLine}Katherine,41,New York{Environment.NewLine}";
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

static void FiltersTextRowsWithWhere()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,role\r\nAda,admin\r\nGrace,user\r\nKatherine,admin\r\n");

    try
    {
        using var output = new MemoryStream();
        CsvRoundTripper.WriteFilteredRows(tempPath, "role=admin", output);

        var result = Encoding.UTF8.GetString(output.ToArray());
        var expected = $"name,role{Environment.NewLine}Ada,admin{Environment.NewLine}Katherine,admin{Environment.NewLine}";
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

static void FiltersNumericRowsWithNotEqualWhere()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,age\r\nAda,36\r\nGrace,29\r\nKatherine,41\r\n");

    try
    {
        using var output = new MemoryStream();
        CsvRoundTripper.WriteFilteredRows(tempPath, "age!=36", output);

        var result = Encoding.UTF8.GetString(output.ToArray());
        var expected = $"name,age{Environment.NewLine}Grace,29{Environment.NewLine}Katherine,41{Environment.NewLine}";
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

static void ReturnsFailureWhenWhereColumnIsMissing()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,age\r\nAda,36\r\n");

    try
    {
        using var output = new MemoryStream();
        using var stderr = new StringWriter();
        var exitCode = SliceApp.Run([tempPath, "where", "city=London"], stderr, output);
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

static void ReturnsFailureWhenWhereExpressionIsInvalid()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,age\r\nAda,36\r\n");

    try
    {
        using var output = new MemoryStream();
        using var stderr = new StringWriter();
        var exitCode = SliceApp.Run([tempPath, "where", "age"], stderr, output);
        if (exitCode == 0)
        {
            throw new Exception("expected non-zero exit code");
        }

        if (!stderr.ToString().Contains("Invalid comparison expression", StringComparison.Ordinal))
        {
            throw new Exception($"unexpected error output: {stderr}");
        }
    }
    finally
    {
        File.Delete(tempPath);
    }
}

static void KeepsOnlyTheFirstFiveDataRowsWithHead()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, """
        name,age
        Ada,36
        Grace,29
        Katherine,41
        Barbara,32
        Dorothy,25
        Frances,38
        Jean,44
        """.ReplaceLineEndings("\r\n"));

    try
    {
        using var output = new MemoryStream();
        using var stderr = new StringWriter();
        var exitCode = SliceApp.Run([tempPath, "head", "5"], stderr, output);
        if (exitCode != 0)
        {
            throw new Exception($"expected success, got exit code {exitCode}: {stderr}");
        }

        var result = Encoding.UTF8.GetString(output.ToArray());
        var expected = $"name,age{Environment.NewLine}Ada,36{Environment.NewLine}Grace,29{Environment.NewLine}Katherine,41{Environment.NewLine}Barbara,32{Environment.NewLine}Dorothy,25{Environment.NewLine}";
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

static void KeepsAllRowsWhenHeadExceedsRowCount()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,age\r\nAda,36\r\nGrace,29\r\n");

    try
    {
        using var output = new MemoryStream();
        using var stderr = new StringWriter();
        var exitCode = SliceApp.Run([tempPath, "head", "5"], stderr, output);
        if (exitCode != 0)
        {
            throw new Exception($"expected success, got exit code {exitCode}: {stderr}");
        }

        var result = Encoding.UTF8.GetString(output.ToArray());
        var expected = $"name,age{Environment.NewLine}Ada,36{Environment.NewLine}Grace,29{Environment.NewLine}";
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

static void ReturnsFailureWhenHeadRowCountIsNotPositive()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,age\r\nAda,36\r\n");

    try
    {
        using var output = new MemoryStream();
        using var stderr = new StringWriter();
        var exitCode = SliceApp.Run([tempPath, "head", "0"], stderr, output);
        if (exitCode == 0)
        {
            throw new Exception("expected non-zero exit code");
        }

        if (!stderr.ToString().Contains("Row count must be a positive integer", StringComparison.Ordinal))
        {
            throw new Exception($"unexpected error output: {stderr}");
        }
    }
    finally
    {
        File.Delete(tempPath);
    }
}

static void SortsNumericRowsDescending()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,age\r\nAda,36\r\nGrace,29\r\nKatherine,41\r\n");

    try
    {
        using var output = new MemoryStream();
        CsvRoundTripper.WriteSortedRows(tempPath, "age", "desc", output);

        var result = Encoding.UTF8.GetString(output.ToArray());
        var expected = $"name,age{Environment.NewLine}Katherine,41{Environment.NewLine}Ada,36{Environment.NewLine}Grace,29{Environment.NewLine}";
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

static void SortsRowsAscendingByDefault()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,age\r\nAda,36\r\nGrace,29\r\nKatherine,41\r\n");

    try
    {
        using var output = new MemoryStream();
        CsvRoundTripper.WriteSortedRows(tempPath, "age", "asc", output);

        var result = Encoding.UTF8.GetString(output.ToArray());
        var expected = $"name,age{Environment.NewLine}Grace,29{Environment.NewLine}Ada,36{Environment.NewLine}Katherine,41{Environment.NewLine}";
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

static void SortsTextRowsDescending()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,role\r\nAda,admin\r\nGrace,user\r\nKatherine,analyst\r\n");

    try
    {
        using var output = new MemoryStream();
        CsvRoundTripper.WriteSortedRows(tempPath, "role", "desc", output);

        var result = Encoding.UTF8.GetString(output.ToArray());
        var expected = $"name,role{Environment.NewLine}Grace,user{Environment.NewLine}Katherine,analyst{Environment.NewLine}Ada,admin{Environment.NewLine}";
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

static void ReturnsFailureWhenSortColumnIsMissing()
{
    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
    File.WriteAllText(tempPath, "name,age\r\nAda,36\r\n");

    try
    {
        using var output = new MemoryStream();
        using var stderr = new StringWriter();
        var exitCode = SliceApp.Run([tempPath, "sort", "city"], stderr, output);
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
