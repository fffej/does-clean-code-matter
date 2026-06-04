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

    [TestMethod]
    public void Run_CountOutputsNumberOfDataRows()
    {
        string csv = "name,age\r\nAlice,31\r\nBob,29\r\nCharlie,40\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "count"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("3\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_CountReturnsZeroWhenThereAreNoDataRows()
    {
        string csv = "name,age\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "count"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("0\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_SumOutputsTotalForNumericColumn()
    {
        string csv = "name,amount\r\nAlice,10.5\r\nBob,4.5\r\nCharlie,5\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "sum", "amount"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("20\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_SumReturnsZeroWhenThereAreNoDataRows()
    {
        string csv = "name,amount\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "sum", "amount"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("0\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_SumFailsWhenColumnIsMissing()
    {
        string csv = "name,age\r\nAlice,31\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "sum", "amount"], output, error);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Column not found: amount");
            Assert.AreEqual(string.Empty, ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_SumFailsWhenAnyIncludedValueIsNotNumeric()
    {
        string csv = "name,amount\r\nAlice,10\r\nBob,not-a-number\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "sum", "amount"], output, error);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Non-numeric value found in column amount: not-a-number");
            Assert.AreEqual(string.Empty, ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_SelectFormatsTabularResultsAsJson()
    {
        string csv = "name,age,city\r\nAlice,31,London\r\nBob,29,Paris\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "--format", "json", "select", "city,name"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());

            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(ReadUtf8(output));
            Assert.AreEqual(2, document.RootElement.GetArrayLength());
            Assert.AreEqual("London", document.RootElement[0].GetProperty("city").GetString());
            Assert.AreEqual("Alice", document.RootElement[0].GetProperty("name").GetString());
            Assert.AreEqual("Paris", document.RootElement[1].GetProperty("city").GetString());
            Assert.AreEqual("Bob", document.RootElement[1].GetProperty("name").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_SelectFormatsTabularResultsAsTable()
    {
        string csv = "name,age,city\r\nAlice,31,London\r\nBob,29,Paris\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "select", "city,name", "--format", "table"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual(
                "+--------+-------+\r\n| city   | name  |\r\n+--------+-------+\r\n| London | Alice |\r\n| Paris  | Bob   |\r\n+--------+-------+\r\n",
                ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_CountFormatsScalarResultsAsJson()
    {
        string csv = "name,age\r\nAlice,31\r\nBob,29\r\nCharlie,40\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "--format", "json", "count"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("3\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_ComposesMultipleCommandsLeftToRight()
    {
        string csv = "name,age,city\r\nAlice,45,London\r\nBob,29,Paris\r\nCara,31,Rome\r\nDan,40,Berlin\r\nEve,35,Madrid\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "where", "age>30", "|", "sort", "age", "|", "head", "3"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual(
                "name,age,city\r\nCara,31,Rome\r\nEve,35,Madrid\r\nDan,40,Berlin\r\n",
                ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_FailsWhenRowBasedCommandFollowsAggregateResult()
    {
        string csv = "name,age\r\nAlice,31\r\nBob,29\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "count", "|", "head", "1"], output, error);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Command 'head' cannot be applied to an aggregate result.");
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
