using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Slice.Tests;

[TestClass]
public sealed class CsvSortAppTests
{
    [TestMethod]
    public void Run_SortsNumericColumnDescending()
    {
        string csv = "name,age,city\r\nAlice,31,London\r\nBob,29,Paris\r\nCharlie,40,Rome\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "sort", "age", "desc"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("name,age,city\r\nCharlie,40,Rome\r\nAlice,31,London\r\nBob,29,Paris\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_SortsAscendingByDefault()
    {
        string csv = "name,age\r\nCharlie,40\r\nAlice,31\r\nBob,29\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "sort", "name"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("name,age\r\nAlice,31\r\nBob,29\r\nCharlie,40\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_UsesTextSortingWhenAnyValueIsNotNumeric()
    {
        string csv = "value\r\n2\r\n10\r\na\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "sort", "value"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("value\r\n10\r\n2\r\na\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_FailsWhenSortColumnIsMissing()
    {
        string csv = "name,age\r\nAlice,31\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "sort", "country"], output, error);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Column not found: country");
            Assert.AreEqual(string.Empty, ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTempFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }

    private static string ReadUtf8(MemoryStream stream)
    {
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
