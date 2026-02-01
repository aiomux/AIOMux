namespace AIOMux.Local.Security;

/// <summary>
/// Service for checking permissions based on configuration.
/// </summary>
public class PermissionService
{
    private readonly Dictionary<string, string> _permissions;

    public PermissionService(Dictionary<string, string> permissions)
    {
        _permissions = permissions ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a capability is allowed, denied, or requires confirmation.
    /// </summary>
    /// <returns>"allow", "deny", or "confirm"</returns>
    public string GetPermission(string capability)
    {
        return _permissions.TryGetValue(capability, out var mode) ? mode : "deny";
    }

    /// <summary>
    /// Returns true if the capability is explicitly allowed.
    /// </summary>
    public bool IsAllowed(string capability)
    {
        return GetPermission(capability).Equals("allow", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true if the capability is denied.
    /// </summary>
    public bool IsDenied(string capability)
    {
        return GetPermission(capability).Equals("deny", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true if the capability requires user confirmation.
    /// </summary>
    public bool RequiresConfirmation(string capability)
    {
        return GetPermission(capability).Equals("confirm", StringComparison.OrdinalIgnoreCase);
    }
}
