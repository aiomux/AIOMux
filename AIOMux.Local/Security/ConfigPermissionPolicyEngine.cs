using AIOMux.Core;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;

namespace AIOMux.Local.Security;

/// <summary>
/// Policy engine that enforces tool access from aiomux.json permission settings.
/// </summary>
public sealed class ConfigPermissionPolicyEngine : IPolicyEngine
{
    private const string PolicyHash = "config-permissions-v1";
    private readonly PermissionService _permissionService;

    public ConfigPermissionPolicyEngine(PermissionService permissionService)
    {
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    public PolicyDecision Evaluate(ToolCall call, AgentContext context)
    {
        if (string.IsNullOrWhiteSpace(call.ToolName))
        {
            return PolicyDecision.Deny("Tool call is missing tool name.", PolicyHash);
        }

        if (_permissionService.IsAllowed(call.ToolName))
        {
            return PolicyDecision.Allow(PolicyHash);
        }

        if (_permissionService.RequiresConfirmation(call.ToolName))
        {
            return PolicyDecision.Deny(
                $"Tool '{call.ToolName}' requires confirmation, which is not yet supported in local runtime.",
                PolicyHash);
        }

        return PolicyDecision.Deny($"Tool '{call.ToolName}' blocked by configured permissions.", PolicyHash);
    }
}
