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
            RunSortNumericDescendingTest();
            RunSortTextAscendingDefaultTest();
            RunSortMissingColumnTest();
            RunHeadFirstNRowsTest();
            RunHeadFewerThanRequestedRowsTest();
            RunHeadInvalidRowCountTest();

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

    private static void RunSortNumericDescendingTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-sort-numeric-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Grace,36,New York\r\n" +
                       "Ada,31,London\r\n" +
                       "Linus,20,Helsinki\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "sort", "age", "desc"], input, output, error);

            AssertEqual(0, exitCode, "sort numeric exit code");
            AssertEqual(expected, output.ToString(), "sort numeric stdout");
            AssertEqual(string.Empty, error.ToString(), "sort numeric stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunSortTextAscendingDefaultTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-sort-text-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Ada,31,London\r\n" +
                       "Grace,36,New York\r\n" +
                       "Linus,20,Helsinki\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "sort", "name"], input, output, error);

            AssertEqual(0, exitCode, "sort text exit code");
            AssertEqual(expected, output.ToString(), "sort text stdout");
            AssertEqual(string.Empty, error.ToString(), "sort text stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunSortMissingColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-sort-missing-column-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age\r\nAda,31\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "sort", "city"], input, output, error);

            AssertEqual(1, exitCode, "sort missing column exit code");
            AssertEqual(string.Empty, output.ToString(), "sort missing column stdout");
            AssertEqual("Column not found: city" + Environment.NewLine, error.ToString(), "sort missing column stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunHeadFirstNRowsTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-head-first-n-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n" +
                       "\"Barbara\",41,\"Palo Alto\"\r\n" +
                       "\"Edsger\",72,\"Amsterdam\"\r\n" +
                       "\"Katherine\",101,\"Langley\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Ada,31,London\r\n" +
                       "Grace,36,New York\r\n" +
                       "Linus,20,Helsinki\r\n" +
                       "Barbara,41,Palo Alto\r\n" +
                       "Edsger,72,Amsterdam\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "head", "5"], input, output, error);

            AssertEqual(0, exitCode, "head first n exit code");
            AssertEqual(expected, output.ToString(), "head first n stdout");
            AssertEqual(string.Empty, error.ToString(), "head first n stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunHeadFewerThanRequestedRowsTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-head-fewer-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age\r\n" +
                       "\"Ada\",31\r\n" +
                       "\"Grace\",36\r\n";
        var expected = "name,age\r\n" +
                       "Ada,31\r\n" +
                       "Grace,36\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "head", "5"], input, output, error);

            AssertEqual(0, exitCode, "head fewer rows exit code");
            AssertEqual(expected, output.ToString(), "head fewer rows stdout");
            AssertEqual(string.Empty, error.ToString(), "head fewer rows stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunHeadInvalidRowCountTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-head-invalid-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age\r\nAda,31\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var zeroExitCode = App.Run([tempFile, "head", "0"], input, output, error);

            AssertEqual(1, zeroExitCode, "head zero exit code");
            AssertEqual(string.Empty, output.ToString(), "head zero stdout");
            AssertEqual("Invalid row count." + Environment.NewLine, error.ToString(), "head zero stderr");

            output.GetStringBuilder().Clear();
            error.GetStringBuilder().Clear();

            var negativeExitCode = App.Run([tempFile, "head", "-3"], input, output, error);

            AssertEqual(1, negativeExitCode, "head negative exit code");
            AssertEqual(string.Empty, output.ToString(), "head negative stdout");
            AssertEqual("Invalid row count." + Environment.NewLine, error.ToString(), "head negative stderr");
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
