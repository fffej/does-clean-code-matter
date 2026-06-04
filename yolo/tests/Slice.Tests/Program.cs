using System.Text;
using Slice;

static class Program
{
    public static int Main()
    {
        try
        {
            RunSelectProjectionTest();
            RunMissingColumnTest();
            RunWhereNumericFilterTest();
            RunWhereTextInequalityTest();

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void RunSelectProjectionTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-select-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n";
        var expected = "age,name\r\n31,Ada\r\n36,Grace\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "select", "age,name"], input, output, error);

            AssertEqual(0, exitCode, "exit code");
            AssertEqual(expected, output.ToString(), "stdout");
            AssertEqual(string.Empty, error.ToString(), "stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunMissingColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-missing-column-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age\r\nAda,31\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "select", "city"], input, output, error);

            AssertEqual(1, exitCode, "missing column exit code");
            AssertEqual(string.Empty, output.ToString(), "missing column stdout");
            AssertEqual("Column not found: city" + Environment.NewLine, error.ToString(), "missing column stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunWhereNumericFilterTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-where-numeric-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Ada,31,London\r\n" +
                       "Grace,36,New York\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "where", "age>30"], input, output, error);

            AssertEqual(0, exitCode, "where numeric exit code");
            AssertEqual(expected, output.ToString(), "where numeric stdout");
            AssertEqual(string.Empty, error.ToString(), "where numeric stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunWhereTextInequalityTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-where-text-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Grace,36,New York\r\n" +
                       "Linus,20,Helsinki\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "where", "city!=London"], input, output, error);

            AssertEqual(0, exitCode, "where text exit code");
            AssertEqual(expected, output.ToString(), "where text stdout");
            AssertEqual(string.Empty, error.ToString(), "where text stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void AssertEqual(string expected, string actual, string label)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                $"{label} mismatch.{Environment.NewLine}Expected:{Environment.NewLine}{expected}{Environment.NewLine}Actual:{Environment.NewLine}{actual}");
        }
    }

    private static void AssertEqual(int expected, int actual, string label)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{label} mismatch. Expected {expected}, got {actual}.");
        }
    }
}
