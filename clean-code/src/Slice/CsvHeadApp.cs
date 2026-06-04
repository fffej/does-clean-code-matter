using System.Text;

namespace Slice;

public static class CsvHeadApp
{
    public static int Run(string csvPath, string rowCountArgument, Stream output, TextWriter error)
    {
        if (!TryParseRowCount(rowCountArgument, out int rowCount))
        {
            error.WriteLine($"Invalid head count: {rowCountArgument}");
            return 1;
        }

        if (!CsvInputReader.TryReadRows(csvPath, error, out List<IReadOnlyList<string>> rows))
        {
            return 1;
        }

        using StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        WriteRows(writer, rows, rowCount);
        writer.Flush();
        output.Flush();
        return 0;
    }

    private static bool TryParseRowCount(string rowCountArgument, out int rowCount)
    {
        return int.TryParse(rowCountArgument, out rowCount) && rowCount > 0;
    }

    private static void WriteRows(TextWriter writer, IReadOnlyList<IReadOnlyList<string>> rows, int rowCount)
    {
        int rowsToWrite = Math.Min(rows.Count, rowCount + 1);
        for (int rowIndex = 0; rowIndex < rowsToWrite; rowIndex++)
        {
            writer.Write(CsvWriter.FormatRow(rows[rowIndex]));
            writer.Write("\r\n");
        }
    }
}
