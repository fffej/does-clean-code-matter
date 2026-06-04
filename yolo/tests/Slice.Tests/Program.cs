using System.Text;
using Slice;

static class Program
{
    public static int Main()
    {
        try
        {
            RunSelectProjectionTest();
            RunJsonFormatProjectionTest();
            RunTableFormatProjectionTest();
            RunMissingColumnTest();
            RunWhereNumericFilterTest();
            RunWhereSortHeadPipelineTest();
            RunWhereTextInequalityTest();
            RunSortNumericDescendingTest();
            RunSortTextAscendingDefaultTest();
            RunSortMissingColumnTest();
            RunHeadFirstNRowsTest();
            RunHeadFewerThanRequestedRowsTest();
            RunHeadInvalidRowCountTest();
            RunDistinctSingleColumnFirstSeenOrderTest();
            RunDistinctMultipleColumnsCompositeKeyTest();
            RunDistinctMissingColumnTest();
            RunCountAllRowsTest();
            RunCountJsonFormatTest();
            RunCountAfterFilterAndLimitTest();
            RunSumAmountColumnTest();
            RunSumMissingColumnTest();
            RunSumNonNumericColumnTest();
            RunGroupByCountFirstSeenOrderTest();
            RunGroupBySumAmountColumnTest();
            RunGroupByMissingColumnTest();
            RunGroupByNonNumericSumColumnTest();

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void RunSelectProjectionTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-select-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n";
        var expected = "age,name\r\n31,Ada\r\n36,Grace\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "select", "age,name"], input, output, error);

            AssertEqual(0, exitCode, "exit code");
            AssertEqual(expected, output.ToString(), "stdout");
            AssertEqual(string.Empty, error.ToString(), "stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunJsonFormatProjectionTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-json-format-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n";
        var expected = "[{\"age\":\"31\",\"name\":\"Ada\"},{\"age\":\"36\",\"name\":\"Grace\"}]\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "--format", "json", "select", "age,name"], input, output, error);

            AssertEqual(0, exitCode, "json format exit code");
            AssertEqual(expected, output.ToString(), "json format stdout");
            AssertEqual(string.Empty, error.ToString(), "json format stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunTableFormatProjectionTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-table-format-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n";
        var expected = "+-----+-------+\r\n" +
                       "| age | name  |\r\n" +
                       "+-----+-------+\r\n" +
                       "| 31  | Ada   |\r\n" +
                       "| 36  | Grace |\r\n" +
                       "+-----+-------+\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "--format", "table", "select", "age,name"], input, output, error);

            AssertEqual(0, exitCode, "table format exit code");
            AssertEqual(expected, output.ToString(), "table format stdout");
            AssertEqual(string.Empty, error.ToString(), "table format stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunMissingColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-missing-column-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age\r\nAda,31\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "select", "city"], input, output, error);

            AssertEqual(1, exitCode, "missing column exit code");
            AssertEqual(string.Empty, output.ToString(), "missing column stdout");
            AssertEqual("Column not found: city" + Environment.NewLine, error.ToString(), "missing column stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunWhereNumericFilterTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-where-numeric-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Ada,31,London\r\n" +
                       "Grace,36,New York\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "where", "age>30"], input, output, error);

            AssertEqual(0, exitCode, "where numeric exit code");
            AssertEqual(expected, output.ToString(), "where numeric stdout");
            AssertEqual(string.Empty, error.ToString(), "where numeric stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunWhereSortHeadPipelineTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-pipeline-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n" +
                       "\"Barbara\",41,\"Palo Alto\"\r\n" +
                       "\"Edsger\",72,\"Amsterdam\"\r\n" +
                       "\"Katherine\",29,\"Langley\"\r\n" +
                       "\"Donald\",55,\"Zurich\"\r\n" +
                       "\"Evelyn\",33,\"Seattle\"\r\n" +
                       "\"Ken\",64,\"Boston\"\r\n" +
                       "\"Margaret\",44,\"Chicago\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Ada,31,London\r\n" +
                       "Evelyn,33,Seattle\r\n" +
                       "Grace,36,New York\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "where", "age>30", "|", "sort", "age", "|", "head", "3"], input, output, error);

            AssertEqual(0, exitCode, "pipeline exit code");
            AssertEqual(expected, output.ToString(), "pipeline stdout");
            AssertEqual(string.Empty, error.ToString(), "pipeline stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunWhereTextInequalityTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-where-text-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Grace,36,New York\r\n" +
                       "Linus,20,Helsinki\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "where", "city!=London"], input, output, error);

            AssertEqual(0, exitCode, "where text exit code");
            AssertEqual(expected, output.ToString(), "where text stdout");
            AssertEqual(string.Empty, error.ToString(), "where text stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunSortNumericDescendingTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-sort-numeric-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Grace,36,New York\r\n" +
                       "Ada,31,London\r\n" +
                       "Linus,20,Helsinki\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "sort", "age", "desc"], input, output, error);

            AssertEqual(0, exitCode, "sort numeric exit code");
            AssertEqual(expected, output.ToString(), "sort numeric stdout");
            AssertEqual(string.Empty, error.ToString(), "sort numeric stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunSortTextAscendingDefaultTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-sort-text-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Ada,31,London\r\n" +
                       "Grace,36,New York\r\n" +
                       "Linus,20,Helsinki\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "sort", "name"], input, output, error);

            AssertEqual(0, exitCode, "sort text exit code");
            AssertEqual(expected, output.ToString(), "sort text stdout");
            AssertEqual(string.Empty, error.ToString(), "sort text stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunSortMissingColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-sort-missing-column-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age\r\nAda,31\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "sort", "city"], input, output, error);

            AssertEqual(1, exitCode, "sort missing column exit code");
            AssertEqual(string.Empty, output.ToString(), "sort missing column stdout");
            AssertEqual("Column not found: city" + Environment.NewLine, error.ToString(), "sort missing column stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunHeadFirstNRowsTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-head-first-n-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age,city\r\n" +
                       "\"Ada\",31,\"London\"\r\n" +
                       "\"Grace\",36,\"New York\"\r\n" +
                       "\"Linus\",20,\"Helsinki\"\r\n" +
                       "\"Barbara\",41,\"Palo Alto\"\r\n" +
                       "\"Edsger\",72,\"Amsterdam\"\r\n" +
                       "\"Katherine\",101,\"Langley\"\r\n";
        var expected = "name,age,city\r\n" +
                       "Ada,31,London\r\n" +
                       "Grace,36,New York\r\n" +
                       "Linus,20,Helsinki\r\n" +
                       "Barbara,41,Palo Alto\r\n" +
                       "Edsger,72,Amsterdam\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "head", "5"], input, output, error);

            AssertEqual(0, exitCode, "head first n exit code");
            AssertEqual(expected, output.ToString(), "head first n stdout");
            AssertEqual(string.Empty, error.ToString(), "head first n stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunHeadFewerThanRequestedRowsTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-head-fewer-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age\r\n" +
                       "\"Ada\",31\r\n" +
                       "\"Grace\",36\r\n";
        var expected = "name,age\r\n" +
                       "Ada,31\r\n" +
                       "Grace,36\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "head", "5"], input, output, error);

            AssertEqual(0, exitCode, "head fewer rows exit code");
            AssertEqual(expected, output.ToString(), "head fewer rows stdout");
            AssertEqual(string.Empty, error.ToString(), "head fewer rows stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunHeadInvalidRowCountTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-head-invalid-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,age\r\nAda,31\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var zeroExitCode = App.Run([tempFile, "head", "0"], input, output, error);

            AssertEqual(1, zeroExitCode, "head zero exit code");
            AssertEqual(string.Empty, output.ToString(), "head zero stdout");
            AssertEqual("Invalid row count." + Environment.NewLine, error.ToString(), "head zero stderr");

            output.GetStringBuilder().Clear();
            error.GetStringBuilder().Clear();

            var negativeExitCode = App.Run([tempFile, "head", "-3"], input, output, error);

            AssertEqual(1, negativeExitCode, "head negative exit code");
            AssertEqual(string.Empty, output.ToString(), "head negative stdout");
            AssertEqual("Invalid row count." + Environment.NewLine, error.ToString(), "head negative stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunDistinctSingleColumnFirstSeenOrderTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-distinct-single-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,city\r\n" +
                       "\"Ada\",\"London\"\r\n" +
                       "\"Grace\",\"New York\"\r\n" +
                       "\"Linus\",\"London\"\r\n" +
                       "\"Barbara\",\"Paris\"\r\n" +
                       "\"Edsger\",\"New York\"\r\n";
        var expected = "city\r\n" +
                       "London\r\n" +
                       "New York\r\n" +
                       "Paris\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "distinct", "city"], input, output, error);

            AssertEqual(0, exitCode, "distinct single exit code");
            AssertEqual(expected, output.ToString(), "distinct single stdout");
            AssertEqual(string.Empty, error.ToString(), "distinct single stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunDistinctMultipleColumnsCompositeKeyTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-distinct-composite-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,city,team\r\n" +
                       "\"Ada\",\"London\",\"A\"\r\n" +
                       "\"Grace\",\"London\",\"A\"\r\n" +
                       "\"Linus\",\"London\",\"B\"\r\n" +
                       "\"Barbara\",\"Paris\",\"A\"\r\n" +
                       "\"Edsger\",\"Paris\",\"A\"\r\n";
        var expected = "city,team\r\n" +
                       "London,A\r\n" +
                       "London,B\r\n" +
                       "Paris,A\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "distinct", "city", "team"], input, output, error);

            AssertEqual(0, exitCode, "distinct composite exit code");
            AssertEqual(expected, output.ToString(), "distinct composite stdout");
            AssertEqual(string.Empty, error.ToString(), "distinct composite stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunDistinctMissingColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-distinct-missing-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,city\r\nAda,London\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "distinct", "country"], input, output, error);

            AssertEqual(1, exitCode, "distinct missing exit code");
            AssertEqual(string.Empty, output.ToString(), "distinct missing stdout");
            AssertEqual("Column not found: country" + Environment.NewLine, error.ToString(), "distinct missing stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunCountAllRowsTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-count-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,amount\r\n" +
                       "Ada,10\r\n" +
                       "Grace,20\r\n" +
                       "Linus,30\r\n" +
                       "Barbara,40\r\n" +
                       "Edsger,50\r\n" +
                       "Katherine,60\r\n" +
                       "Donald,70\r\n" +
                       "Evelyn,80\r\n" +
                       "Ken,90\r\n" +
                       "Margaret,100\r\n";
        var expected = "10\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "count"], input, output, error);

            AssertEqual(0, exitCode, "count exit code");
            AssertEqual(expected, output.ToString(), "count stdout");
            AssertEqual(string.Empty, error.ToString(), "count stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunCountJsonFormatTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-count-json-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,amount\r\n" +
                       "Ada,10\r\n" +
                       "Grace,20\r\n" +
                       "Linus,30\r\n";
        var expected = "3\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "--format", "json", "count"], input, output, error);

            AssertEqual(0, exitCode, "count json format exit code");
            AssertEqual(expected, output.ToString(), "count json format stdout");
            AssertEqual(string.Empty, error.ToString(), "count json format stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunCountAfterFilterAndLimitTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-count-chain-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,amount\r\n" +
                       "Ada,10\r\n" +
                       "Grace,20\r\n" +
                       "Linus,30\r\n" +
                       "Barbara,40\r\n" +
                       "Edsger,50\r\n";
        var expected = "2\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "where", "amount>20", "head", "2", "count"], input, output, error);

            AssertEqual(0, exitCode, "count chain exit code");
            AssertEqual(expected, output.ToString(), "count chain stdout");
            AssertEqual(string.Empty, error.ToString(), "count chain stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunSumAmountColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-sum-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,amount\r\n" +
                       "Ada,10\r\n" +
                       "Grace,20\r\n" +
                       "Linus,30\r\n";
        var expected = "60\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "sum", "amount"], input, output, error);

            AssertEqual(0, exitCode, "sum exit code");
            AssertEqual(expected, output.ToString(), "sum stdout");
            AssertEqual(string.Empty, error.ToString(), "sum stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunSumMissingColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-sum-missing-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,amount\r\nAda,10\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "sum", "missing"], input, output, error);

            AssertEqual(1, exitCode, "sum missing exit code");
            AssertEqual(string.Empty, output.ToString(), "sum missing stdout");
            AssertEqual("Column not found: missing" + Environment.NewLine, error.ToString(), "sum missing stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunSumNonNumericColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-sum-nonnumeric-{Guid.NewGuid():N}.csv");
        var inputCsv = "name,amount\r\n" +
                       "Ada,10\r\n" +
                       "Grace,NaN\r\n" +
                       "Linus,30\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "sum", "amount"], input, output, error);

            AssertEqual(1, exitCode, "sum non-numeric exit code");
            AssertEqual(string.Empty, output.ToString(), "sum non-numeric stdout");
            AssertEqual("Column contains non-numeric values: amount" + Environment.NewLine, error.ToString(), "sum non-numeric stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunGroupByCountFirstSeenOrderTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-groupby-count-{Guid.NewGuid():N}.csv");
        var inputCsv = "city,amount\r\n" +
                       "\"London\",10\r\n" +
                       "\"New York\",20\r\n" +
                       "\"London\",30\r\n" +
                       "\"Paris\",40\r\n" +
                       "\"New York\",50\r\n";
        var expected = "city,count\r\n" +
                       "London,2\r\n" +
                       "New York,2\r\n" +
                       "Paris,1\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "groupby", "city", "count"], input, output, error);

            AssertEqual(0, exitCode, "groupby count exit code");
            AssertEqual(expected, output.ToString(), "groupby count stdout");
            AssertEqual(string.Empty, error.ToString(), "groupby count stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunGroupBySumAmountColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-groupby-sum-{Guid.NewGuid():N}.csv");
        var inputCsv = "city,amount\r\n" +
                       "\"London\",10\r\n" +
                       "\"New York\",20.5\r\n" +
                       "\"London\",30\r\n" +
                       "\"Paris\",40\r\n" +
                       "\"New York\",50\r\n";
        var expected = "city,sum\r\n" +
                       "London,40\r\n" +
                       "New York,70.5\r\n" +
                       "Paris,40\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "groupby", "city", "sum", "amount"], input, output, error);

            AssertEqual(0, exitCode, "groupby sum exit code");
            AssertEqual(expected, output.ToString(), "groupby sum stdout");
            AssertEqual(string.Empty, error.ToString(), "groupby sum stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunGroupByMissingColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-groupby-missing-{Guid.NewGuid():N}.csv");
        var inputCsv = "city,amount\r\n\"London\",10\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "groupby", "country", "count"], input, output, error);

            AssertEqual(1, exitCode, "groupby missing exit code");
            AssertEqual(string.Empty, output.ToString(), "groupby missing stdout");
            AssertEqual("Column not found: country" + Environment.NewLine, error.ToString(), "groupby missing stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunGroupByNonNumericSumColumnTest()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice-groupby-nonnumeric-{Guid.NewGuid():N}.csv");
        var inputCsv = "city,amount\r\n" +
                       "\"London\",10\r\n" +
                       "\"New York\",oops\r\n" +
                       "\"London\",30\r\n";

        try
        {
            File.WriteAllText(tempFile, inputCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            using var input = new StringReader(string.Empty);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = App.Run([tempFile, "groupby", "city", "sum", "amount"], input, output, error);

            AssertEqual(1, exitCode, "groupby non-numeric exit code");
            AssertEqual(string.Empty, output.ToString(), "groupby non-numeric stdout");
            AssertEqual("Column contains non-numeric values: amount" + Environment.NewLine, error.ToString(), "groupby non-numeric stderr");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void AssertEqual(string expected, string actual, string label)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                $"{label} mismatch.{Environment.NewLine}Expected:{Environment.NewLine}{expected}{Environment.NewLine}Actual:{Environment.NewLine}{actual}");
        }
    }

    private static void AssertEqual(int expected, int actual, string label)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{label} mismatch. Expected {expected}, got {actual}.");
        }
    }
}
