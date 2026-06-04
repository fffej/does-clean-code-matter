namespace Slice;

internal static class CsvHeaderLookup
{
    public static int FindHeaderIndex(IReadOnlyList<string> header, string columnName)
    {
        for (int index = 0; index < header.Count; index++)
        {
            if (string.Equals(header[index], columnName, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
