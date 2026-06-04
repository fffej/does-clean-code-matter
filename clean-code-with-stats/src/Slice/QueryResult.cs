namespace Slice;

internal sealed record ExecutionOutcome(QueryResult? Result, string? ErrorMessage)
{
    public static ExecutionOutcome Success(QueryResult result)
    {
        return new ExecutionOutcome(result, null);
    }

    public static ExecutionOutcome Failure(string errorMessage)
    {
        return new ExecutionOutcome(null, errorMessage);
    }
}

internal abstract record QueryResult;

internal sealed record TableQueryResult(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) : QueryResult;

internal sealed record ScalarQueryResult(decimal Value) : QueryResult;
