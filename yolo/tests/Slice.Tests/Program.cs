using System.Text;
using Slice;

static class Program
{
    public static int Main()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-roundtrip-{Guid.NewGuid():N}.csv");
        var expected = "name,quote,notes\r\n" +
                       "\"Ada\",\"Hello, world\",\"Line 1\r\nLine 2\"\r\n" +
                       "\"Grace\",\"He said \"\"hi\"\"\",\"\r\nleading newline\"\r\n";

        try
        {
            File.WriteAllText(tempFile, expected, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile], input, output, error);

            AssertEqual(0, exitCode, "exit code");
            AssertEqual(expected, output.ToString(), "stdout");
            AssertEqual(string.Empty, error.ToString(), "stderr");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
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
