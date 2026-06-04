using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Slice;

namespace Slice.Tests;

[TestClass]
public sealed class SliceApplicationTests
{
    [TestMethod]
    public async Task RunAsync_WithCsvFile_WritesTheSameBytesToStandardOutput()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var expected = "Name,Age\r\nAda,36\r\n\"Lovelace, Ada\",37\r\n";
        var expectedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(expected);

        try
        {
            await File.WriteAllBytesAsync(tempFile, expectedBytes);

            await using var output = new MemoryStream();
            var app = new SliceApplication(output, TextWriter.Null);

            var exitCode = await app.RunAsync([tempFile]);

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
    public async Task RunAsync_WithMissingFile_ReturnsAnError()
    {
        await using var output = new MemoryStream();
        var error = new StringWriter();
        var app = new SliceApplication(output, error);

        var exitCode = await app.RunAsync(["missing.csv"]);

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
        StringAssert.Contains(error.ToString(), "Usage: slice <csv-file>");
        Assert.AreEqual(0, output.Length);
    }
}
