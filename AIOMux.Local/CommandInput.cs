namespace AIOMux.Local;

internal sealed record CommandInput
{
    public string Verb { get; init; } = string.Empty;

    public IReadOnlyList<string> Parameters { get; init; } = [];
}
