using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Slice.Tests;

[TestClass]
public sealed class CsvDistinctAppTests
{
    [TestMethod]
    public void Run_WritesUniqueValuesInFirstSeenOrder()
    {
        string csv = "name,city\r\nAlice,London\r\nBob,Paris\r\nCara,London\r\nDan,Rome\r\nEve,Paris\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "distinct", "city"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("city\r\nLondon\r\nParis\r\nRome\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_UsesAllRequestedColumnsToDetermineDistinctRows()
    {
        string csv = "name,city,country\r\nAlice,London,UK\r\nBob,London,UK\r\nCara,London,FR\r\nDan,Paris,FR\r\nEve,Paris,FR\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "distinct", "city", "country"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("city,country\r\nLondon,UK\r\nLondon,FR\r\nParis,FR\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_FailsWhenAnyDistinctColumnIsMissing()
    {
        string csv = "name,city\r\nAlice,London\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "distinct", "country"], output, error);

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
