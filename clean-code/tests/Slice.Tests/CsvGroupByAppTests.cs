using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Slice.Tests;

[TestClass]
public sealed class CsvGroupByAppTests
{
    [TestMethod]
    public void Run_CountsRowsPerGroupInFirstSeenOrder()
    {
        string csv = "name,city,amount\r\nAlice,London,10\r\nBob,Paris,20\r\nCara,London,30\r\nDan,Rome,40\r\nEve,Paris,50\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "groupby", "city", "count"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("city,count\r\nLondon,2\r\nParis,2\r\nRome,1\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_SumsValuesPerGroup()
    {
        string csv = "name,city,amount\r\nAlice,London,10.5\r\nBob,Paris,20\r\nCara,London,4.5\r\nDan,Rome,7\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "groupby", "city", "sum", "amount"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("city,sum_amount\r\nLondon,15\r\nParis,20\r\nRome,7\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_FailsWhenGroupColumnIsMissing()
    {
        string csv = "name,city,amount\r\nAlice,London,10\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "groupby", "country", "count"], output, error);

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
    public void Run_FailsWhenSumColumnIsMissing()
    {
        string csv = "name,city,amount\r\nAlice,London,10\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "groupby", "city", "sum", "total"], output, error);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Column not found: total");
            Assert.AreEqual(string.Empty, ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_FailsWhenSumValueIsNotNumeric()
    {
        string csv = "name,city,amount\r\nAlice,London,10\r\nBob,London,not-a-number\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "groupby", "city", "sum", "amount"], output, error);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Non-numeric value found in column amount: not-a-number");
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
