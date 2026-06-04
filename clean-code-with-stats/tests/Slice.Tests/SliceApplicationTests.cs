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
        StringAssert.Contains(error.ToString(), "Usage: slice <csv-file> select <columns>");
        Assert.AreEqual(0, output.Length);
    }
}
