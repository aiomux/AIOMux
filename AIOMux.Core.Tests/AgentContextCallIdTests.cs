using AIOMux.Core.Interfaces;
using Xunit;

namespace AIOMux.Core.Tests;

public class AgentContextCallIdTests
{
    private class TestTool : ITool
    {
        private readonly Func<string, Task<string>> _implementation;

        public string Name { get; }

        public TestTool(string name, Func<string, Task<string>> implementation)
        {
            Name = name;
            _implementation = implementation;
        }

        public Task<string> ExecuteAsync(string input) => _implementation(input);
    }

    [Fact]
    public async Task ExecuteToolAsync_DeterministicCallId_ForSameRunAndArgs()
    {
        var context = new AgentContext();
        context.Variables["runId"] = "run-1";
        context.Variables["stepIndex"] = 0;

        var tool = new TestTool("echo", input => Task.FromResult(input));
        context.Tools.Add("echo", tool);

        var first = await context.ExecuteToolAsync("echo", "{\"value\":1}");
        var second = await context.ExecuteToolAsync("echo", "{\"value\":1}");
        var third = await context.ExecuteToolAsync("echo", "{\"value\":2}");

        Assert.Equal(first.CallId, second.CallId);
        Assert.NotEqual(first.CallId, third.CallId);
    }
}
