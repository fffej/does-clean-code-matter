using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Slice.Tests;

[TestClass]
public sealed class CsvPassthroughAppTests
{
    [TestMethod]
    public void Run_WritesInputCsvUnchanged()
    {
        string csv = "Name,Age\r\nAlice,31\r\nBob,29\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual(csv, ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_PreservesQuotedCsvContentExactly()
    {
        string csv = "Header1,Header2\r\n\"A,B\",\"He said \"\"hi\"\"\"\r\n\"Line1\r\nLine2\",Value\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual(csv, ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_SelectsColumnsInRequestedOrder()
    {
        string csv = "name,age,city\r\nAlice,31,London\r\nBob,29,Paris\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "select", "city,name"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("city,name\r\nLondon,Alice\r\nParis,Bob\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_SelectsColumnsFromQuotedCsvContent()
    {
        string csv = "name,notes,city\r\nAlice,\"Line1\r\nLine2\",London\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "select", "notes,name"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("notes,name\r\n\"Line1\r\nLine2\",Alice\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_FailsWhenSelectedColumnIsMissing()
    {
        string csv = "name,age,city\r\nAlice,31,London\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "select", "country"], output, error);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Column not found: country");
            Assert.AreEqual(string.Empty, ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_HeadKeepsHeaderAndFirstNDataRows()
    {
        string csv = "name,age\r\nAlice,31\r\nBob,29\r\nCharlie,40\r\nDora,25\r\nEve,33\r\nFrank,28\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "head", "5"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual(
                "name,age\r\nAlice,31\r\nBob,29\r\nCharlie,40\r\nDora,25\r\nEve,33\r\n",
                ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_HeadOutputsAllRowsWhenFewerThanRequested()
    {
        string csv = "name,age\r\nAlice,31\r\nBob,29\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "head", "5"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual(csv, ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    public void Run_HeadFailsWhenCountIsNotPositive(string rowCountArgument)
    {
        string csv = "name,age\r\nAlice,31\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "head", rowCountArgument], output, error);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), $"Invalid head count: {rowCountArgument}");
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
