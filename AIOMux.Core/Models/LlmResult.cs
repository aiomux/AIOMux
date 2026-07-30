namespace AIOMux.Core.Models;

/// <summary>
/// Structured result from an LLM request.
/// </summary>
public sealed record LlmResult
{
    public required bool Success { get; init; }

    public string? Content { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}