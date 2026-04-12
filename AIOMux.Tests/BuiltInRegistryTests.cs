using AIOMux.Connectors;
using AIOMux.Core;

namespace AIOMux.Tests;

public sealed class BuiltInRegistryTests
{
    [Fact]
    public void BuiltInConnectorRegistry_ResolveType_Console_ReturnsConsoleConnectorType()
    {
        var resolvedType = BuiltInConnectorRegistry.ResolveType("console");

        Assert.Equal("ConsoleConnector", resolvedType.Name);
    }

    [Fact]
    public void BuiltInConnectorRegistry_ResolveType_Unknown_ThrowsClearError()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BuiltInConnectorRegistry.ResolveType("not-a-connector"));

        Assert.Contains("Unknown connector type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuiltInAgentRegistry_ResolveType_Echo_ReturnsEchoAgentType()
    {
        var resolvedType = BuiltInAgentRegistry.ResolveType("echo");

        Assert.Equal(typeof(EchoAgent), resolvedType);
    }

    [Fact]
    public void BuiltInAgentRegistry_ResolveType_Gauntlet_ReturnsGauntletAgentType()
    {
        var resolvedType = BuiltInAgentRegistry.ResolveType("gauntlet");

        Assert.Equal(typeof(GauntletAgent), resolvedType);
    }

    [Fact]
    public void BuiltInAgentRegistry_ResolveType_Unknown_ThrowsClearError()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BuiltInAgentRegistry.ResolveType("not-an-agent"));

        Assert.Contains("Unknown agent", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
