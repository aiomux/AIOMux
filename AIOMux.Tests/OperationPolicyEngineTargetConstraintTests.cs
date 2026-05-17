using AIOMux.Core.Models;
using AIOMux.Core.Policy;

namespace AIOMux.Tests;

public sealed class OperationPolicyEngineTargetConstraintTests
{
    [Fact]
    public void Evaluate_Allows_FilePathWithinAllowedRoot()
    {
        var engine = CreateEngine(
            ToolOperation.Read,
            new ToolPolicyConstraints { AllowedPaths = [Path.GetFullPath("C:/temp/aiomux")] });

        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Read],
            [new ToolTarget(ToolTargetKind.FilePath, Path.Combine("C:/temp/aiomux", "logs", "app.log"))]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "FileSystemTool", Input = "read" }, analysis, new AgentContext());

        Assert.True(decision.Allowed, decision.DenyReason);
    }

    [Fact]
    public void Evaluate_Denies_FilePathInDeniedRoot()
    {
        var engine = CreateEngine(
            ToolOperation.Read,
            new ToolPolicyConstraints { DeniedPaths = [Path.GetFullPath("C:/Windows")] });

        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Read],
            [new ToolTarget(ToolTargetKind.FilePath, Path.Combine("C:/Windows", "System32", "drivers", "etc", "hosts"))]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "FileSystemTool", Input = "read" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.Contains("denied", decision.DenyReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Denies_TraversalAttemptOutsideAllowedRoot()
    {
        var engine = CreateEngine(
            ToolOperation.Read,
            new ToolPolicyConstraints { AllowedPaths = [Path.GetFullPath("C:/apps/logs")] });

        var traversalPath = Path.Combine("C:/apps/logs", "..", "..", "Windows", "system.ini");
        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Read],
            [new ToolTarget(ToolTargetKind.FilePath, traversalPath)]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "FileSystemTool", Input = "read" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.Contains("outside allowed", decision.DenyReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Allows_UrlHostWhenAllowed()
    {
        var constraints = new ToolPolicyConstraints
        {
            AllowedHosts = ["localhost", "api.internal.local"],
            AllowedSchemes = ["https"]
        };

        var engine = CreateEngine(ToolOperation.Network, constraints, toolName: "HttpTool");
        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Network],
            [new ToolTarget(ToolTargetKind.Url, "https://localhost:5000/health")]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "HttpTool", Input = "get" }, analysis, new AgentContext());

        Assert.True(decision.Allowed, decision.DenyReason);
    }

    [Fact]
    public void Evaluate_Denies_UrlHostWhenDenied()
    {
        var constraints = new ToolPolicyConstraints
        {
            AllowedHosts = ["localhost", "api.internal.local"],
            DeniedHosts = ["169.254.169.254"]
        };

        var engine = CreateEngine(ToolOperation.Network, constraints, toolName: "HttpTool");
        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Network],
            [new ToolTarget(ToolTargetKind.Url, "http://169.254.169.254/latest/meta-data")]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "HttpTool", Input = "get" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.Contains("denied", decision.DenyReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Denies_UnknownTargetKind()
    {
        var engine = CreateEngine(
            ToolOperation.Read,
            new ToolPolicyConstraints { AllowedPaths = [Path.GetFullPath("C:/apps/logs")] });

        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Read],
            [new ToolTarget(ToolTargetKind.None, "value")]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "FileSystemTool", Input = "read" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.Contains("Unknown target kind", decision.DenyReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Denies_MissingTargetWhenConstraintsExist()
    {
        var engine = CreateEngine(
            ToolOperation.Read,
            new ToolPolicyConstraints { AllowedPaths = [Path.GetFullPath("C:/apps/logs")] });

        var analysis = ToolExecutionAnalysis.Recognized([ToolOperation.Read], []);

        var decision = engine.Evaluate(new ToolCall { ToolName = "FileSystemTool", Input = "read" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.Contains("missing required targets", decision.DenyReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Denies_MalformedUrl()
    {
        var constraints = new ToolPolicyConstraints { AllowedHosts = ["localhost"] };
        var engine = CreateEngine(ToolOperation.Network, constraints, toolName: "HttpTool");

        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Network],
            [new ToolTarget(ToolTargetKind.Url, "not-a-url")]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "HttpTool", Input = "get" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.Contains("Malformed URL", decision.DenyReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Denies_UrlPortWhenDenied()
    {
        var constraints = new ToolPolicyConstraints
        {
            AllowedHosts = ["api.example.com"],
            AllowedSchemes = ["https"],
            DeniedPorts = [8080]
        };

        var engine = CreateEngine(ToolOperation.Network, constraints, toolName: "HttpTool");
        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Network],
            [new ToolTarget(ToolTargetKind.Url, "https://api.example.com:8080/resource")]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "HttpTool", Input = "get" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.Contains("denied", decision.DenyReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Allows_UrlPortWhenInAllowedSet()
    {
        var constraints = new ToolPolicyConstraints
        {
            AllowedHosts = ["api.example.com"],
            AllowedSchemes = ["https"],
            AllowedPorts = [443]
        };

        var engine = CreateEngine(ToolOperation.Network, constraints, toolName: "HttpTool");
        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Network],
            [new ToolTarget(ToolTargetKind.Url, "https://api.example.com:443/resource")]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "HttpTool", Input = "get" }, analysis, new AgentContext());

        Assert.True(decision.Allowed, decision.DenyReason);
    }

    [Fact]
    public void Evaluate_Denies_UrlPortWhenNotInAllowedSet()
    {
        var constraints = new ToolPolicyConstraints
        {
            AllowedHosts = ["api.example.com"],
            AllowedSchemes = ["https"],
            AllowedPorts = [443]
        };

        var engine = CreateEngine(ToolOperation.Network, constraints, toolName: "HttpTool");
        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Network],
            [new ToolTarget(ToolTargetKind.Url, "https://api.example.com:9443/resource")]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "HttpTool", Input = "get" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.Contains("not allowed", decision.DenyReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Denies_CommandWithDeniedArgument()
    {
        var constraints = new ToolPolicyConstraints
        {
            AllowedCommands = ["git"],
            DeniedArguments = ["--force"]
        };

        var engine = CreateEngine(ToolOperation.CommandExecute, constraints, toolName: "ShellTool");
        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.CommandExecute],
            [new ToolTarget(ToolTargetKind.Command, "git push --force")]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "ShellTool", Input = "push" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.Contains("denied", decision.DenyReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Allows_CommandWithoutDeniedArgument()
    {
        var constraints = new ToolPolicyConstraints
        {
            AllowedCommands = ["git"],
            DeniedArguments = ["--force"]
        };

        var engine = CreateEngine(ToolOperation.CommandExecute, constraints, toolName: "ShellTool");
        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.CommandExecute],
            [new ToolTarget(ToolTargetKind.Command, "git push origin main")]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "ShellTool", Input = "push" }, analysis, new AgentContext());

        Assert.True(decision.Allowed, decision.DenyReason);
    }

    [Theory]
    [InlineData(ToolOperation.Delete)]
    [InlineData(ToolOperation.Write)]
    [InlineData(ToolOperation.HttpPost)]
    [InlineData(ToolOperation.CommandExecute)]
    [InlineData(ToolOperation.ProcessKill)]
    [InlineData(ToolOperation.ProcessStart)]
    [InlineData(ToolOperation.ServiceStop)]
    [InlineData(ToolOperation.ServiceRestart)]
    [InlineData(ToolOperation.RegistryWrite)]
    [InlineData(ToolOperation.SecretRead)]
    public void Evaluate_Denies_DangerousOperation_WithoutExplicitConstraints(ToolOperation dangerousOp)
    {
        var policy = new Dictionary<string, ToolPolicyDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["DangerousTool"] = new ToolPolicyDefinition(
                [dangerousOp],
                new Dictionary<ToolOperation, ToolPolicyConstraints>())
        };

        var engine = new OperationPolicyEngine(policy, "test-policy");
        var analysis = ToolExecutionAnalysis.Recognized(
            [dangerousOp],
            [new ToolTarget(ToolTargetKind.FilePath, "C:/temp/file.txt")]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "DangerousTool", Input = "action" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.Contains("requires explicit constraints", decision.DenyReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Allows_DangerousOperation_WhenConstraintsPresent()
    {
        var constraints = new ToolPolicyConstraints
        {
            AllowedPaths = [Path.GetFullPath("C:/apps/data")]
        };

        var engine = CreateEngine(ToolOperation.Write, constraints, toolName: "WriteTool");
        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Write],
            [new ToolTarget(ToolTargetKind.FilePath, Path.Combine("C:/apps/data", "output.txt"))]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "WriteTool", Input = "write" }, analysis, new AgentContext());

        Assert.True(decision.Allowed, decision.DenyReason);
    }

    [Fact]
    public void PolicyDenyReason_Contains_TargetAndOperation()
    {
        var engine = CreateEngine(
            ToolOperation.Read,
            new ToolPolicyConstraints { DeniedPaths = [Path.GetFullPath("C:/secret")] });

        var analysis = ToolExecutionAnalysis.Recognized(
            [ToolOperation.Read],
            [new ToolTarget(ToolTargetKind.FilePath, Path.Combine("C:/secret", "key.pem"))]);

        var decision = engine.Evaluate(new ToolCall { ToolName = "FileSystemTool", Input = "read" }, analysis, new AgentContext());

        Assert.False(decision.Allowed);
        Assert.NotNull(decision.DenyReason);
        Assert.NotEmpty(decision.DenyReason!);
    }

    private static OperationPolicyEngine CreateEngine(
        ToolOperation operation,
        ToolPolicyConstraints constraints,
        string toolName = "FileSystemTool")
    {
        var policy = new Dictionary<string, ToolPolicyDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [toolName] = new ToolPolicyDefinition(
                [operation],
                new Dictionary<ToolOperation, ToolPolicyConstraints>
                {
                    [operation] = constraints
                })
        };

        return new OperationPolicyEngine(policy, "test-policy");
    }
}
