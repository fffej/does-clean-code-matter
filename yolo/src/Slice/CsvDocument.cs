using System.Text;

namespace Slice;

internal sealed class CsvDocument
{
    internal CsvDocument(
        IReadOnlyList<string> header,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string lineEnding,
        bool endsWithLineEnding)
    {
        Header = header;
        Rows = rows;
        LineEnding = lineEnding;
        EndsWithLineEnding = endsWithLineEnding;
    }

    public IReadOnlyList<string> Header { get; }

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    public string LineEnding { get; }

    public bool EndsWithLineEnding { get; }

    public static bool TryParse(string input, out CsvDocument? document, out string error)
    {
        document = null;
        error = string.Empty;

        try
        {
            var rows = new List<IReadOnlyList<string>>();
            var currentRow = new List<string>();
            var currentField = new StringBuilder();
            CsvState state = CsvState.StartOfField;
            string? lineEnding = null;
            bool endsWithLineEnding = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                endsWithLineEnding = false;

                switch (state)
                {
                    case CsvState.StartOfField:
                        if (c == ',')
                        {
                            currentRow.Add(string.Empty);
                            break;
                        }

                        if (c == '\r' || c == '\n')
                        {
                            currentRow.Add(string.Empty);
                            CommitRow(rows, currentRow);
                            lineEnding ??= DetectLineEnding(input, i);
                            endsWithLineEnding = true;
                            if (c == '\r' && i + 1 < input.Length && input[i + 1] == '\n')
                            {
                                i++;
                            }

                            break;
                        }

                        if (c == '"')
                        {
                            state = CsvState.InQuotedField;
                            break;
                        }

                        currentField.Append(c);
                        state = CsvState.InUnquotedField;
                        break;

                    case CsvState.InUnquotedField:
                        if (c == ',')
                        {
                            currentRow.Add(currentField.ToString());
                            currentField.Clear();
                            state = CsvState.StartOfField;
                            break;
                        }

                        if (c == '\r' || c == '\n')
                        {
                            currentRow.Add(currentField.ToString());
                            currentField.Clear();
                            CommitRow(rows, currentRow);
                            lineEnding ??= DetectLineEnding(input, i);
                            endsWithLineEnding = true;
                            state = CsvState.StartOfField;
                            if (c == '\r' && i + 1 < input.Length && input[i + 1] == '\n')
                            {
                                i++;
                            }

                            break;
                        }

                        currentField.Append(c);
                        break;

                    case CsvState.InQuotedField:
                        if (c == '"')
                        {
                            if (i + 1 < input.Length && input[i + 1] == '"')
                            {
                                currentField.Append('"');
                                i++;
                                break;
                            }

                            state = CsvState.AfterQuotedField;
                            break;
                        }

                        currentField.Append(c);
                        break;

                    case CsvState.AfterQuotedField:
                        if (c == ',')
                        {
                            currentRow.Add(currentField.ToString());
                            currentField.Clear();
                            state = CsvState.StartOfField;
                            break;
                        }

                        if (c == '\r' || c == '\n')
                        {
                            currentRow.Add(currentField.ToString());
                            currentField.Clear();
                            CommitRow(rows, currentRow);
                            lineEnding ??= DetectLineEnding(input, i);
                            endsWithLineEnding = true;
                            state = CsvState.StartOfField;
                            if (c == '\r' && i + 1 < input.Length && input[i + 1] == '\n')
                            {
                                i++;
                            }

                            break;
                        }

                        throw new FormatException("Invalid CSV format.");
                }
            }

            if (state == CsvState.InQuotedField)
            {
                throw new FormatException("Unterminated quoted field.");
            }

            if (state == CsvState.InUnquotedField || state == CsvState.AfterQuotedField || currentRow.Count > 0)
            {
                if (state == CsvState.InUnquotedField || state == CsvState.AfterQuotedField || currentRow.Count > 0 || currentField.Length > 0)
                {
                    currentRow.Add(currentField.ToString());
                    CommitRow(rows, currentRow);
                }
            }

            if (rows.Count == 0)
            {
                error = "Input CSV is empty.";
                return false;
            }

            document = new CsvDocument(
                rows[0],
                rows.Skip(1).ToArray(),
                lineEnding ?? Environment.NewLine,
                endsWithLineEnding);
            return true;
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static void WriteSelection(
        CsvDocument document,
        IReadOnlyList<int> selectedIndexes,
        IReadOnlyList<IReadOnlyList<string>> rows,
        TextWriter output)
    {
        string separator = document.LineEnding;

        WriteRow(output, document.Header, selectedIndexes);

        for (int i = 0; i < rows.Count; i++)
        {
            output.Write(separator);
            WriteRow(output, rows[i], selectedIndexes);
        }

        if (document.EndsWithLineEnding)
        {
            output.Write(separator);
        }
    }

    public static void WriteDocument(CsvDocument document, IReadOnlyList<IReadOnlyList<string>> rows, TextWriter output)
    {
        string separator = document.LineEnding;

        WriteRow(output, document.Header);

        for (int i = 0; i < rows.Count; i++)
        {
            output.Write(separator);
            WriteRow(output, rows[i]);
        }

        if (document.EndsWithLineEnding)
        {
            output.Write(separator);
        }
    }

    private static void WriteRow(TextWriter output, IReadOnlyList<string> row, IReadOnlyList<int> selectedIndexes)
    {
        for (int i = 0; i < selectedIndexes.Count; i++)
        {
            if (i > 0)
            {
                output.Write(',');
            }

            int index = selectedIndexes[i];
            string value = index < row.Count ? row[index] : string.Empty;
            output.Write(Escape(value));
        }
    }

    private static void WriteRow(TextWriter output, IReadOnlyList<string> row)
    {
        for (int i = 0; i < row.Count; i++)
        {
            if (i > 0)
            {
                output.Write(',');
            }

            output.Write(Escape(row[i]));
        }
    }

    private static string Escape(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        bool needsQuotes = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == ',' || c == '"' || c == '\r' || c == '\n')
            {
                needsQuotes = true;
                break;
            }
        }

        if (!needsQuotes)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (char c in value)
        {
            if (c == '"')
            {
                builder.Append("\"\"");
            }
            else
            {
                builder.Append(c);
            }
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static void CommitRow(List<IReadOnlyList<string>> rows, List<string> currentRow)
    {
        rows.Add(currentRow.ToArray());
        currentRow.Clear();
    }

    private static string DetectLineEnding(string input, int index)
    {
        if (input[index] == '\r' && index + 1 < input.Length && input[index + 1] == '\n')
        {
            return "\r\n";
        }

        return input[index] == '\r' ? "\r" : "\n";
    }

    private enum CsvState
    {
        StartOfField,
        InUnquotedField,
        InQuotedField,
        AfterQuotedField,
    }
}
