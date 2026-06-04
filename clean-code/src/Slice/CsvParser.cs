namespace Slice;

public static class CsvParser
{
    public static List<IReadOnlyList<string>> Parse(TextReader reader)
    {
        var rows = new List<IReadOnlyList<string>>();
        var currentRow = new List<string>();
        var currentField = new StringWriter();
        bool insideQuotedField = false;
        bool fieldWasStarted = false;
        bool lastTokenWasComma = false;

        while (true)
        {
            int value = reader.Read();
            if (value < 0)
            {
                break;
            }

            char character = (char)value;

            if (insideQuotedField)
            {
                if (character == '"')
                {
                    int next = reader.Peek();
                    if (next == '"')
                    {
                        reader.Read();
                        currentField.Write('"');
                        fieldWasStarted = true;
                        continue;
                    }

                    insideQuotedField = false;
                    continue;
                }

                currentField.Write(character);
                fieldWasStarted = true;
                continue;
            }

            if (character == ',')
            {
                AppendField(currentRow, currentField);
                fieldWasStarted = false;
                lastTokenWasComma = true;
                continue;
            }

            if (character == '\r')
            {
                if (reader.Peek() == '\n')
                {
                    reader.Read();
                }

                AppendField(currentRow, currentField);
                AppendRow(rows, currentRow);
                fieldWasStarted = false;
                lastTokenWasComma = false;
                continue;
            }

            if (character == '\n')
            {
                AppendField(currentRow, currentField);
                AppendRow(rows, currentRow);
                fieldWasStarted = false;
                lastTokenWasComma = false;
                continue;
            }

            if (character == '"' && currentField.GetStringBuilder().Length == 0)
            {
                insideQuotedField = true;
                fieldWasStarted = true;
                continue;
            }

            currentField.Write(character);
            fieldWasStarted = true;
            lastTokenWasComma = false;
        }

        if (insideQuotedField)
        {
            throw new FormatException("CSV input ended inside a quoted field.");
        }

        if (fieldWasStarted || lastTokenWasComma || currentRow.Count > 0)
        {
            AppendField(currentRow, currentField);
            AppendRow(rows, currentRow);
        }

        return rows;
    }

    private static void AppendField(List<string> row, StringWriter field)
    {
        row.Add(field.ToString());
        field.GetStringBuilder().Clear();
    }

    private static void AppendRow(List<IReadOnlyList<string>> rows, List<string> row)
    {
        rows.Add(row.ToArray());
        row.Clear();
    }
}
