namespace AppInsightsMcp.Configuration;

/// <summary>
/// Configuration for connecting to Application Insights via Azure Monitor APIs.
/// </summary>
public sealed class AppInsightsConfig
{
    /// <summary>
    /// The Log Analytics workspace ID (GUID) where Application Insights data resides.
    /// Find this in Azure Portal under your Application Insights resource > Properties > Workspace ID.
    /// </summary>
    public string WorkspaceId { get; set; } = string.Empty;

    /// <summary>
    /// The Azure AD tenant ID where the Application Insights resource lives.
    /// Required when the resource is in a different tenant than your default login.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// The full Azure resource ID of the Application Insights component for metrics queries.
    /// Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/microsoft.insights/components/{name}
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;
}
