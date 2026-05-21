using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using System.Collections.Immutable;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Tests;

public sealed class LLMClientResolverTests
{
    [Fact]
    public void TryGet_WhenProfileExists_ReturnsClient()
    {
        var expected = new FakeLlmClient("ollama", "qwen2.5:7b");
        var profiles = new Dictionary<string, ILLMClient>
        {
            ["fast-local"] = expected
        };
        var resolver = new LLMClientResolver(profiles);

        var client = resolver.TryGet("fast-local");

        Assert.Same(expected, client);
    }

    [Fact]
    public void TryGet_WhenProfileMissing_ReturnsNull()
    {
        var profiles = new Dictionary<string, ILLMClient>
        {
            ["default"] = new FakeLlmClient("ollama", "llama3")
        };
        var resolver = new LLMClientResolver(profiles);

        var client = resolver.TryGet("vision-local");

        Assert.Null(client);
    }

    [Fact]
    public void GetRequired_WhenProfileExists_ReturnsClient()
    {
        var expected = new FakeLlmClient("ollama", "qwen2.5:7b");
        var profiles = new Dictionary<string, ILLMClient>
        {
            ["fast-local"] = expected
        };
        var resolver = new LLMClientResolver(profiles);

        var client = resolver.GetRequired("fast-local");

        Assert.Same(expected, client);
    }

    [Fact]
    public void GetRequired_WhenProfileMissing_ThrowsWithProfileNameInMessage()
    {
        var profiles = new Dictionary<string, ILLMClient>
        {
            ["default"] = new FakeLlmClient("ollama", "llama3")
        };
        var resolver = new LLMClientResolver(profiles);

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.GetRequired("vision-local"));

        Assert.Contains("vision-local", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRequired_WhenProfileNameBlank_ThrowsArgumentException()
    {
        var profiles = new Dictionary<string, ILLMClient>
        {
            ["default"] = new FakeLlmClient("ollama", "llama3")
        };
        var resolver = new LLMClientResolver(profiles);

        Assert.Throws<ArgumentException>(() => resolver.GetRequired("  "));
    }

    [Fact]
    public void GetRequired_WhenProfileNameNull_ThrowsArgumentException()
    {
        var profiles = new Dictionary<string, ILLMClient>
        {
            ["default"] = new FakeLlmClient("ollama", "llama3")
        };
        var resolver = new LLMClientResolver(profiles);

        Assert.Throws<ArgumentException>(() => resolver.GetRequired(null!));
    }

    [Fact]
    public void Constructor_WhenDuplicateProfiles_Throws()
    {
        var profiles = new Dictionary<string, ILLMClient>
        {
            ["fast-local"] = new FakeLlmClient("ollama", "qwen2.5:7b"),
            ["FAST-LOCAL"] = new FakeLlmClient("ollama", "llava")
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new LLMClientResolver(profiles));

        Assert.Contains("Duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FAST-LOCAL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentFactoryContext_PassesResolverToAgent()
    {
        var profiles = new Dictionary<string, ILLMClient>
        {
            ["test-profile"] = new FakeLlmClient("ollama", "test-model")
        };
        var resolver = new LLMClientResolver(profiles);
        var context = new AgentFactoryContext
        {
            LlmResolver = resolver,
            Configuration = new Dictionary<string, object> { ["key"] = "value" }
        };
        var testAgent = new TestAgentWithContext();

        var createdAgent = testAgent.CreateAgent(context);

        Assert.NotNull(createdAgent);
        var typedAgent = Assert.IsType<TestAgentWithContext>(createdAgent);
        Assert.Same(resolver, typedAgent.ReceivedResolver);
        Assert.Equal("value", typedAgent.ReceivedConfiguration["key"]);
    }

    [Fact]
    public void ValidateRequiredLlmProfiles_WhenResolverNonNullAndProfileMissing_ThrowsWithAgentAndProfileName()
    {
        var profiles = new Dictionary<string, ILLMClient>
        {
            ["default"] = new FakeLlmClient("ollama", "llama3")
        };
        var resolver = new LLMClientResolver(profiles);
        var metadata = new AgentMetadata
        {
            Name = "TestAgent",
            RequiredLlmProfiles = ["missing-profile"]
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => AgentManager.ValidateRequiredLlmProfiles(metadata, resolver, null));

        Assert.Contains("TestAgent", ex.Message, StringComparison.Ordinal);
        Assert.Contains("missing-profile", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRequiredLlmProfiles_WhenResolverNull_DoesNotThrow()
    {
        var metadata = new AgentMetadata
        {
            Name = "TestAgent",
            RequiredLlmProfiles = ["some-profile"]
        };

        var exception = Record.Exception(
            () => AgentManager.ValidateRequiredLlmProfiles(metadata, null, null));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateRequiredLlmProfiles_WhenAllProfilesExist_DoesNotThrow()
    {
        var profiles = new Dictionary<string, ILLMClient>
        {
            ["profile-one"] = new FakeLlmClient("ollama", "model1"),
            ["profile-two"] = new FakeLlmClient("ollama", "model2")
        };
        var resolver = new LLMClientResolver(profiles);
        var metadata = new AgentMetadata
        {
            Name = "TestAgent",
            RequiredLlmProfiles = ["profile-one", "profile-two"]
        };

        var exception = Record.Exception(
            () => AgentManager.ValidateRequiredLlmProfiles(metadata, resolver, null));

        Assert.Null(exception);
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

    private sealed class TestAgentWithContext : IAgent
    {
        public string Name => "test-agent";
        public ILLMClientResolver? ReceivedResolver { get; private set; }
        public Dictionary<string, object>? ReceivedConfiguration { get; private set; }

        public IAgent CreateAgent(AgentFactoryContext context)
        {
            ReceivedResolver = context.LlmResolver;
            ReceivedConfiguration = context.Configuration;
            return this;
        }

        public Task<StepExecutionResult> ExecuteAsync(
            ImmutableDictionary<string, object?> inputs,
            ExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new StepExecutionResult { Success = true, Output = "test" });
    }
}
