namespace AIOMux.Core.Models;

/// <summary>
/// Identifies how an execution plan was produced.
/// </summary>
public enum PlanSource
{
    Static,
    Generated,
    ReplayFork,
    AdHoc
}
