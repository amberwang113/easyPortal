namespace DevPortal.Models;

public class ApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// The Azure ARM API version to use for all requests
    /// </summary>
    public string ApiVersion { get; set; } = "2024-11-01";
    
    /// <summary>
    /// The Azure subscription ID to query for resources
    /// </summary>
    public string SubscriptionId { get; set; } = string.Empty;
    
    /// <summary>
    /// The Azure resource group to query for resources
    /// </summary>
    public string ResourceGroup { get; set; } = string.Empty;
    
    /// <summary>
    /// Authentication type: "None", "AzureAd", or "Certificate"
    /// </summary>
    public string AuthType { get; set; } = "None";
    
    public AzureAdSettings AzureAd { get; set; } = new();
    
    public CertificateSettings Certificate { get; set; } = new();
}

public class AzureAdSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
}

public class CertificateSettings
{
    public string Thumbprint { get; set; } = string.Empty;
    public string StoreName { get; set; } = "My";
    public string StoreLocation { get; set; } = "CurrentUser";
}
