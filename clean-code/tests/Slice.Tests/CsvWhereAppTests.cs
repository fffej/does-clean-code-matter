using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Slice.Tests;

[TestClass]
public sealed class CsvWhereAppTests
{
    [TestMethod]
    public void Run_FiltersRowsByNumericComparisonAndPreservesHeader()
    {
        string csv = "name,age,city\r\nAlice,31,London\r\nBob,29,Paris\r\nCharlie,40,Rome\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "where", "age>30"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("name,age,city\r\nAlice,31,London\r\nCharlie,40,Rome\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_UsesTextComparisonWhenEitherSideIsNotNumeric()
    {
        string csv = "value\r\n2\r\n9a\r\n11\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "where", "value>10"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual("value\r\n9a\r\n11\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [DataTestMethod]
    [DataRow("=", "31", "Alice,31,London")]
    [DataRow("!=", "29", "Alice,31,London\r\nCharlie,40,Rome")]
    [DataRow("<", "31", "Bob,29,Paris")]
    [DataRow(">=", "31", "Alice,31,London\r\nCharlie,40,Rome")]
    [DataRow("<=", "29", "Bob,29,Paris")]
    public void Run_SupportsAllComparisonOperators(string operatorToken, string literalValue, string expectedRows)
    {
        string csv = "name,age,city\r\nAlice,31,London\r\nBob,29,Paris\r\nCharlie,40,Rome\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "where", $"age{operatorToken}{literalValue}"], output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(string.Empty, error.ToString());
            Assert.AreEqual($"name,age,city\r\n{expectedRows}\r\n", ReadUtf8(output));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_FailsWhenWhereExpressionIsInvalid()
    {
        string csv = "name,age\r\nAlice,31\r\n";
        string path = CreateTempFile(csv);

        try
        {
            using var output = new MemoryStream();
            using var error = new StringWriter();

            int exitCode = CsvPassthroughApp.Run([path, "where", "age"], output, error);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Invalid where expression: age");
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
