namespace DevPortal.Models;

public class ApiSettings
{
    /// <summary>
    /// The Azure ARM API version to use for all requests
    /// </summary>
    public string ApiVersion { get; set; } = "2024-11-01";
    
    /// <summary>
    /// Authentication mode: "ARM" or "Private"
    /// - ARM: Uses DefaultAzureCredential for Azure Resource Manager (recommended for production)
    /// - Private: Uses client certificate authentication against a private geomaster
    /// </summary>
    public string AuthType { get; set; } = "ARM";
    
    /// <summary>
    /// ARM mode settings - used when AuthType is "ARM"
    /// </summary>
    public ArmSettings ARM { get; set; } = new();
    
    /// <summary>
    /// Private mode settings - used when AuthType is "Private"
    /// </summary>
    public PrivateSettings Private { get; set; } = new();
    
    // Computed properties that return the appropriate values based on AuthType
    public string BaseUrl => AuthType.Equals("ARM", StringComparison.OrdinalIgnoreCase) 
        ? ARM.BaseUrl 
        : Private.BaseUrl;
    
    public string SubscriptionId => AuthType.Equals("ARM", StringComparison.OrdinalIgnoreCase) 
        ? ARM.SubscriptionId 
        : Private.SubscriptionId;
    
    public string ResourceGroup => AuthType.Equals("ARM", StringComparison.OrdinalIgnoreCase) 
        ? ARM.ResourceGroup 
        : Private.ResourceGroup;
}

/// <summary>
/// Settings for ARM mode (Azure Resource Manager with DefaultAzureCredential)
/// </summary>
public class ArmSettings
{
    public string BaseUrl { get; set; } = "https://management.azure.com";
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
}

/// <summary>
/// Settings for Private mode (certificate-based auth against private geomaster)
/// </summary>
public class PrivateSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public CertificateSettings Certificate { get; set; } = new();
}

public class CertificateSettings
{
    public string Thumbprint { get; set; } = string.Empty;
    public string StoreName { get; set; } = "My";
    public string StoreLocation { get; set; } = "CurrentUser";
}

// Keep for backwards compatibility but not actively used
public class AzureAdSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
}
