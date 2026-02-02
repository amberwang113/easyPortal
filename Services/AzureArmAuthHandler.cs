using Azure.Core;
using Azure.Identity;

namespace DevPortal.Services;

/// <summary>
/// A DelegatingHandler that adds Azure ARM bearer token authentication to HTTP requests.
/// Uses DefaultAzureCredential which supports multiple authentication methods:
/// - Environment variables (AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_CLIENT_SECRET)
/// - Managed Identity (when running in Azure)
/// - Visual Studio / VS Code credentials
/// - Azure CLI credentials
/// - Azure PowerShell credentials
/// </summary>
public class AzureArmAuthHandler : DelegatingHandler
{
    private readonly TokenCredential _credential;
    private readonly string[] _scopes;
    private readonly ILogger<AzureArmAuthHandler> _logger;

    public AzureArmAuthHandler(ILogger<AzureArmAuthHandler> logger)
    {
        _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = false,
            ExcludeVisualStudioCredential = false,
            ExcludeVisualStudioCodeCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeAzurePowerShellCredential = false,
            ExcludeInteractiveBrowserCredential = true // Don't pop up browser during API calls
        });
        
        // Azure Resource Manager scope
        _scopes = ["https://management.azure.com/.default"];
        _logger = logger;
        
        _logger.LogInformation("AzureArmAuthHandler initialized with DefaultAzureCredential");
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Get a token for ARM
            var tokenRequestContext = new TokenRequestContext(_scopes);
            var accessToken = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            
            _logger.LogDebug("Acquired ARM token, expires at {ExpiresOn}", accessToken.ExpiresOn);
            
            // Add the bearer token to the request
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", 
                accessToken.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire ARM access token. Ensure you're logged in via Azure CLI, VS, or have proper credentials configured.");
            throw;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
