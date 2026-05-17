using AIOMux.Core;
using AIOMux.Core.Interfaces;

namespace AIOMux.Tests;

public sealed class LLMClientResolverTests
{
    [Fact]
    public void Resolve_WhenProfileExists_ReturnsClient()
    {
        var expected = new FakeLlmClient("ollama", "qwen2.5:7b");
        var resolver = new LLMClientResolver(
        [
            new KeyValuePair<string, ILLMClient>("fast-local", expected)
        ]);

        var client = resolver.Resolve("fast-local");

        Assert.Same(expected, client);
    }

    [Fact]
    public void Resolve_WhenProfileMissing_ThrowsUnknownProfile()
    {
        var resolver = new LLMClientResolver(
        [
            new KeyValuePair<string, ILLMClient>("default", new FakeLlmClient("ollama", "llama3"))
        ]);

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("vision-local"));

        Assert.Contains("Unknown LLM profile 'vision-local'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenDuplicateProfilesExist_ThrowsDuplicateProfileError()
    {
        var profiles = new List<KeyValuePair<string, ILLMClient>>
        {
            new("fast-local", new FakeLlmClient("ollama", "qwen2.5:7b")),
            new("FAST-LOCAL", new FakeLlmClient("ollama", "llava"))
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new LLMClientResolver(profiles));

        Assert.Contains("Duplicate LLM profile 'FAST-LOCAL'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeLlmClient(string provider, string model) : ILLMClient
    {
        public string Provider => provider;
        public string Model => model;
        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(prompt);
        public Task<string> CompleteAsync(string userInput, string systemPrompt, CancellationToken cancellationToken = default)
            => Task.FromResult(userInput);
    }
}
