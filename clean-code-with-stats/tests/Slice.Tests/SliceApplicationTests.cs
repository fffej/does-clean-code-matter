using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Slice;

namespace Slice.Tests;

[TestClass]
public sealed class SliceApplicationTests
{
    [TestMethod]
    public async Task RunAsync_WithSelectCommand_WritesOnlySelectedColumnsInRequestedOrder()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age,city",
            "Ada,36,London",
            "\"Lovelace, Ada\",37,\"New York, NY\"",
            string.Empty
        ]);
        var expected = string.Join("\r\n", [
            "age,name",
            "36,Ada",
            "37,\"Lovelace, Ada\"",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);
        var expectedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expected);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var app = new SliceApplication(output, TextWriter.Null);

            var exitCode = await app.RunAsync([tempFile, "select", "age,name"]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(expectedBytes, output.ToArray());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithMissingColumn_ReturnsAnError()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age,city",
            "Ada,36,London",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var error = new StringWriter();
            var app = new SliceApplication(output, error);

            var exitCode = await app.RunAsync([tempFile, "select", "name,height"]);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Column not found: height");
            Assert.AreEqual(0, output.Length);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithMissingFile_ReturnsAnError()
    {
        await using var output = new MemoryStream();
        var error = new StringWriter();
        var app = new SliceApplication(output, error);

        var exitCode = await app.RunAsync(["missing.csv", "select", "name"]);

        Assert.AreEqual(1, exitCode);
        StringAssert.Contains(error.ToString(), "File not found");
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task RunAsync_WithNoArguments_ShowsUsageAndReturnsFailure()
    {
        await using var output = new MemoryStream();
        var error = new StringWriter();
        var app = new SliceApplication(output, error);

        var exitCode = await app.RunAsync([]);

        Assert.AreEqual(1, exitCode);
        StringAssert.Contains(error.ToString(), "Usage: slice <csv-file> select <columns> | where <expression> | sort <column> [asc|desc] | head <count>");
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task RunAsync_WithHeadCommand_WritesTheHeaderAndFirstNDataRows()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age,city",
            "Ada,36,London",
            "Bob,29,Paris",
            "Carol,31,Berlin",
            "Dave,30,Rome",
            "Eve,25,Oslo",
            "Frank,41,Dublin",
            string.Empty
        ]);
        var expected = string.Join("\r\n", [
            "name,age,city",
            "Ada,36,London",
            "Bob,29,Paris",
            "Carol,31,Berlin",
            "Dave,30,Rome",
            "Eve,25,Oslo",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);
        var expectedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expected);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var app = new SliceApplication(output, TextWriter.Null);

            var exitCode = await app.RunAsync([tempFile, "head", "5"]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(expectedBytes, output.ToArray());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithHeadCommandAndFewerRowsThanRequested_WritesAllRows()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age",
            "Ada,36",
            "Bob,29",
            string.Empty
        ]);
        var expected = string.Join("\r\n", [
            "name,age",
            "Ada,36",
            "Bob,29",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);
        var expectedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expected);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var app = new SliceApplication(output, TextWriter.Null);

            var exitCode = await app.RunAsync([tempFile, "head", "5"]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(expectedBytes, output.ToArray());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithHeadCommandAndNonPositiveCount_ReturnsAnError()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age",
            "Ada,36",
            "Bob,29",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var error = new StringWriter();
            var app = new SliceApplication(output, error);

            var exitCode = await app.RunAsync([tempFile, "head", "0"]);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Invalid row count: 0");
            Assert.AreEqual(0, output.Length);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithWhereCommand_WritesOnlyMatchingRowsAndKeepsHeader()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age,city",
            "Ada,36,London",
            "Bob,29,Paris",
            "Carol,31,Berlin",
            "Dave,30,Rome",
            string.Empty
        ]);
        var expected = string.Join("\r\n", [
            "name,age,city",
            "Ada,36,London",
            "Carol,31,Berlin",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);
        var expectedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expected);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var app = new SliceApplication(output, TextWriter.Null);

            var exitCode = await app.RunAsync([tempFile, "where", "age>30"]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(expectedBytes, output.ToArray());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithWhereCommand_UsesTextComparisonForNonNumericValues()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age",
            "Ada,36",
            "Bob,29",
            "Ada,40",
            string.Empty
        ]);
        var expected = string.Join("\r\n", [
            "name,age",
            "Ada,36",
            "Ada,40",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);
        var expectedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expected);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var app = new SliceApplication(output, TextWriter.Null);

            var exitCode = await app.RunAsync([tempFile, "where", "name=Ada"]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(expectedBytes, output.ToArray());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithWhereCommandAndNoMatches_WritesOnlyTheHeader()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age",
            "Ada,36",
            "Bob,29",
            string.Empty
        ]);
        var expected = string.Join("\r\n", [
            "name,age",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);
        var expectedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expected);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var app = new SliceApplication(output, TextWriter.Null);

            var exitCode = await app.RunAsync([tempFile, "where", "age>100"]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(expectedBytes, output.ToArray());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithSortCommandAndExplicitDescendingDirection_SortsNumericValuesFromHighestToLowest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age,city",
            "Ada,36,London",
            "Bob,29,Paris",
            "Carol,31,Berlin",
            "Dave,40,Rome",
            string.Empty
        ]);
        var expected = string.Join("\r\n", [
            "name,age,city",
            "Dave,40,Rome",
            "Ada,36,London",
            "Carol,31,Berlin",
            "Bob,29,Paris",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);
        var expectedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expected);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var app = new SliceApplication(output, TextWriter.Null);

            var exitCode = await app.RunAsync([tempFile, "sort", "age", "desc"]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(expectedBytes, output.ToArray());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithSortCommandAndNoDirection_UsesAscendingOrder()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age",
            "Ada,36",
            "Bob,29",
            "Carol,31",
            string.Empty
        ]);
        var expected = string.Join("\r\n", [
            "name,age",
            "Bob,29",
            "Carol,31",
            "Ada,36",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);
        var expectedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expected);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var app = new SliceApplication(output, TextWriter.Null);

            var exitCode = await app.RunAsync([tempFile, "sort", "age"]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(expectedBytes, output.ToArray());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithSortCommandAndTextValues_SortsLexicographically()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age",
            "Zoe,36",
            "Ada,29",
            "Bob,31",
            string.Empty
        ]);
        var expected = string.Join("\r\n", [
            "name,age",
            "Ada,29",
            "Bob,31",
            "Zoe,36",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);
        var expectedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expected);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var app = new SliceApplication(output, TextWriter.Null);

            var exitCode = await app.RunAsync([tempFile, "sort", "name"]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(expectedBytes, output.ToArray());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_WithSortCommandAndMissingColumn_ReturnsAnError()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var input = string.Join("\r\n", [
            "name,age",
            "Ada,36",
            "Bob,29",
            string.Empty
        ]);
        var inputBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input);

        try
        {
            await File.WriteAllBytesAsync(tempFile, inputBytes);

            await using var output = new MemoryStream();
            var error = new StringWriter();
            var app = new SliceApplication(output, error);

            var exitCode = await app.RunAsync([tempFile, "sort", "height"]);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "Column not found: height");
            Assert.AreEqual(0, output.Length);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
