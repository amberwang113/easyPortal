namespace DevPortal.Models;

/// <summary>
/// Settings for EasyAgent configuration that will be applied to web apps
/// </summary>
public class EasyAgentSettings
{
    public string WEBSITE_EASYAGENT_FOUNDRY_AGENTID { get; set; } = string.Empty;
    public string WEBSITE_EASYAGENT_FOUNDRY_CHAT_MODEL { get; set; } = "gpt-4o";
    public string WEBSITE_EASYAGENT_FOUNDRY_EMBEDDING_MODEL { get; set; } = "text-embedding-3-small";
    public string WEBSITE_EASYAGENT_FOUNDRY_ENDPOINT { get; set; } = string.Empty;
    public string WEBSITE_EASYAGENT_FOUNDRY_OPENAPISPEC { get; set; } = string.Empty;
    public string WEBSITE_EASYAGENT_SITECONTEXT_DB_ENDPOINT { get; set; } = string.Empty;
    public string WEBSITE_EASYAGENT_SITECONTEXT_DB_NAME { get; set; } = "RAGDatabase";
    public string WEBSITE_MANAGED_CLIENT_ID { get; set; } = string.Empty;
    
    /// <summary>
    /// The full ARM resource ID of the User Assigned Managed Identity to assign to web apps.
    /// Example: /subscriptions/{subId}/resourcegroups/{rg}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{name}
    /// </summary>
    public string UserAssignedIdentityResourceId { get; set; } = string.Empty;
}
