namespace Slice;

public static class CsvWriter
{
    public static string FormatRow(IReadOnlyList<string> fields)
    {
        var builder = new System.Text.StringBuilder();
        for (int index = 0; index < fields.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(FormatField(fields[index]));
        }

        return builder.ToString();
    }

    private static string FormatField(string field)
    {
        bool mustQuote = field.Contains(',') || field.Contains('"') || field.Contains('\r') || field.Contains('\n');
        if (!mustQuote)
        {
            return field;
        }

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
