namespace Slice;

public abstract record QueryResult
{
    public sealed record Scalar(string Value) : QueryResult;

    public sealed record Table(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows) : QueryResult;
}
