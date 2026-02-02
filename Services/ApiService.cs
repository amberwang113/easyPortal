using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Xml.Linq;
using DevPortal.Models;
using Microsoft.Extensions.Options;

namespace DevPortal.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, IOptions<ApiSettings> settings, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Appends the API version query parameter to an endpoint
    /// </summary>
    private string WithApiVersion(string endpoint)
    {
        var separator = endpoint.Contains('?') ? '&' : '?';
        return $"{endpoint}{separator}api-version={_settings.ApiVersion}";
    }

    #region Web Apps

    public async Task<List<WebApp>> GetWebAppsAsync()
    {
        try
        {
            var subscriptionId = _settings.SubscriptionId;
            if (string.IsNullOrEmpty(subscriptionId))
            {
                _logger.LogWarning("No subscription ID configured");
                return new List<WebApp>();
            }

            var resourceGroup = _settings.ResourceGroup;
            if (string.IsNullOrEmpty(resourceGroup))
            {
                _logger.LogWarning("No resource group configured");
                return new List<WebApp>();
            }

            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites");
            _logger.LogInformation("Fetching web apps from API: {BaseAddress}{Endpoint}", _httpClient.BaseAddress, endpoint);
            
            var response = await _httpClient.GetAsync(endpoint);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("API request failed with status {StatusCode}: {ReasonPhrase}. Response: {ErrorContent}", 
                    (int)response.StatusCode, response.ReasonPhrase, errorContent);
                return new List<WebApp>();
            }
            
            var listResponse = await response.Content.ReadFromJsonAsync<ListResponse<WebAppResource>>();
            if (listResponse?.Value != null && listResponse.Value.Count > 0)
            {
                var webApps = listResponse.Value.Select(MapWebAppResourceToWebApp).ToList();
                _logger.LogInformation("Retrieved {Count} web apps from API", webApps.Count);
                return webApps;
            }
            
            _logger.LogWarning("Empty response from API");
            return new List<WebApp>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch web apps from API");
            return new List<WebApp>();
        }
    }

    private static WebApp MapWebAppResourceToWebApp(WebAppResource webAppResource)
    {
        // Use resource group from properties if available, otherwise extract from ID
        var resourceGroup = !string.IsNullOrEmpty(webAppResource.Properties.ResourceGroup) 
            ? webAppResource.Properties.ResourceGroup 
            : ExtractResourceGroupFromId(webAppResource.Id);
        
        // Determine runtime from site config
        var runtime = DetermineRuntime(webAppResource.Properties.SiteConfig, webAppResource.Kind);
        
        return new WebApp
        {
            // Use Name as the Id for navigation (e.g., /webapps/myapp)
            Id = webAppResource.Name,
            Name = webAppResource.Name,
            // Store the full ARM resource ID for API calls
            ArmId = webAppResource.Id,
            ResourceGroup = resourceGroup,
            Location = webAppResource.Location,
            Status = MapState(webAppResource.Properties.State),
            Url = string.IsNullOrEmpty(webAppResource.Properties.DefaultHostName) 
                ? string.Empty 
                : $"https://{webAppResource.Properties.DefaultHostName}",
            Runtime = runtime,
            HttpsOnly = webAppResource.Properties.HttpsOnly,
            AlwaysOn = webAppResource.Properties.SiteConfig?.AlwaysOn ?? false,
            HttpVersion = webAppResource.Properties.SiteConfig?.Http20Enabled == true ? "2.0" : "1.1",
            WebSocketsEnabled = webAppResource.Properties.SiteConfig?.WebSocketsEnabled ?? false,
            LastModifiedDate = webAppResource.Properties.LastModifiedTimeUtc ?? DateTime.UtcNow,
            CurrentInstances = webAppResource.Properties.SiteConfig?.NumberOfWorkers ?? 1
        };
    }

    private static string ExtractResourceGroupFromId(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return string.Empty;
        
        var parts = resourceId.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
            {
                return parts[i + 1];
            }
        }
        return string.Empty;
    }

    private static string DetermineRuntime(SiteConfig? siteConfig, string? kind)
    {
        if (siteConfig == null) return "Unknown";
        
        // Check Linux runtime
        if (!string.IsNullOrEmpty(siteConfig.LinuxFxVersion))
            return siteConfig.LinuxFxVersion;
        
        // Check Windows runtime
        if (!string.IsNullOrEmpty(siteConfig.WindowsFxVersion))
            return siteConfig.WindowsFxVersion;
        
        // Check specific runtime versions
        if (!string.IsNullOrEmpty(siteConfig.JavaVersion))
            return $"Java {siteConfig.JavaVersion}";
        
        if (!string.IsNullOrEmpty(siteConfig.PythonVersion))
            return $"Python {siteConfig.PythonVersion}";
        
        if (!string.IsNullOrEmpty(siteConfig.NodeVersion))
            return $"Node {siteConfig.NodeVersion}";
        
        if (!string.IsNullOrEmpty(siteConfig.PhpVersion))
            return $"PHP {siteConfig.PhpVersion}";
        
        if (!string.IsNullOrEmpty(siteConfig.NetFrameworkVersion))
            return $".NET {siteConfig.NetFrameworkVersion}";
        
        // Fallback based on kind
        if (!string.IsNullOrEmpty(kind))
        {
            if (kind.Contains("linux", StringComparison.OrdinalIgnoreCase))
                return "Linux";
            if (kind.Contains("functionapp", StringComparison.OrdinalIgnoreCase))
                return "Function App";
        }
        
        return "Windows";
    }

    private static string MapState(string state)
    {
        return state?.ToLowerInvariant() switch
        {
            "running" => "Running",
            "stopped" => "Stopped",
            _ => state ?? "Unknown"
        };
    }

    public async Task<WebApp?> GetWebAppAsync(string id)
    {
        try
        {
            _logger.LogInformation("Fetching web app {Id} from API", id);
            
            var subscriptionId = _settings.SubscriptionId;
            var resourceGroup = _settings.ResourceGroup;
            
            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
            {
                _logger.LogWarning("Subscription ID or Resource Group not configured");
                return null;
            }
            
            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{id}");
            var response = await _httpClient.GetAsync(endpoint);
            
            if (response.IsSuccessStatusCode)
            {
                var webAppResource = await response.Content.ReadFromJsonAsync<WebAppResource>();
                if (webAppResource != null)
                {
                    return MapWebAppResourceToWebApp(webAppResource);
                }
            }
            
            _logger.LogWarning("Web app {Id} not found, status: {Status}", id, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch web app {Id} from API", id);
            return null;
        }
    }

    public async Task<WebApp> CreateWebAppAsync(WebApp webApp)
    {
        try
        {
            _logger.LogInformation("Creating web app via API");
            var response = await _httpClient.PostAsJsonAsync("/api/webapps", webApp);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<WebApp>() ?? webApp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create web app via API");
            throw;
        }
    }

    public async Task<WebApp> UpdateWebAppAsync(string id, WebApp webApp)
    {
        try
        {
            _logger.LogInformation("Updating web app {Id} via API", id);
            var response = await _httpClient.PutAsJsonAsync($"/api/webapps/{id}", webApp);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<WebApp>() ?? webApp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update web app {Id} via API", id);
            throw;
        }
    }

    public async Task<bool> DeleteWebAppAsync(string id)
    {
        try
        {
            _logger.LogInformation("Deleting web app {Id} via API", id);
            var response = await _httpClient.DeleteAsync($"/api/webapps/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete web app {Id} via API", id);
            throw;
        }
    }

    public async Task<bool> StartWebAppAsync(string id)
    {
        try
        {
            _logger.LogInformation("Starting web app {Id} via API", id);
            var response = await _httpClient.PostAsync($"/api/webapps/{id}/start", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start web app {Id} via API", id);
            throw;
        }
    }

    public async Task<bool> StopWebAppAsync(string id)
    {
        try
        {
            _logger.LogInformation("Stopping web app {Id} via API", id);
            var response = await _httpClient.PostAsync($"/api/webapps/{id}/stop", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop web app {Id} via API", id);
            throw;
        }
    }

    public async Task<bool> RestartWebAppAsync(string id)
    {
        try
        {
            _logger.LogInformation("Restarting web app {Id} via API", id);
            var response = await _httpClient.PostAsync($"/api/webapps/{id}/restart", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart web app {Id} via API", id);
            throw;
        }
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Gets environment variables (app settings) for a web app from the Azure API
    /// </summary>
    public async Task<List<EnvironmentVariable>> GetEnvironmentVariablesAsync(string webAppName)
    {
        try
        {
            var subscriptionId = _settings.SubscriptionId;
            var resourceGroup = _settings.ResourceGroup;

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
            {
                _logger.LogWarning("Subscription ID or Resource Group not configured");
                return new List<EnvironmentVariable>();
            }

            // Use the POST endpoint to list app settings (required for getting values)
            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}/config/appsettings/list");
            _logger.LogInformation("Fetching app settings from: {Endpoint}", endpoint);

            var response = await _httpClient.PostAsync(endpoint, null);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to fetch app settings. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                return new List<EnvironmentVariable>();
            }

            var appSettingsResource = await response.Content.ReadFromJsonAsync<AppSettingsResource>();
            if (appSettingsResource?.Properties == null)
            {
                return new List<EnvironmentVariable>();
            }

            var result = appSettingsResource.Properties.Select(kvp => new EnvironmentVariable
            {
                Name = kvp.Key,
                Value = kvp.Value,
                Source = DetermineSettingSource(kvp.Value),
                IsSlotSetting = false,
                IsValueHidden = true
            }).OrderBy(e => e.Name).ToList();

            _logger.LogInformation("Retrieved {Count} environment variables", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch environment variables for {WebAppName}", webAppName);
            return new List<EnvironmentVariable>();
        }
    }

    /// <summary>
    /// Gets connection strings for a web app from the Azure API
    /// </summary>
    public async Task<List<ConnectionStringEntry>> GetConnectionStringsAsync(string webAppName)
    {
        try
        {
            var subscriptionId = _settings.SubscriptionId;
            var resourceGroup = _settings.ResourceGroup;

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
            {
                _logger.LogWarning("Subscription ID or Resource Group not configured");
                return new List<ConnectionStringEntry>();
            }

            // Use the POST endpoint to list connection strings (required for getting values)
            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}/config/connectionstrings/list");
            _logger.LogInformation("Fetching connection strings from: {Endpoint}", endpoint);

            var response = await _httpClient.PostAsync(endpoint, null);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to fetch connection strings. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                return new List<ConnectionStringEntry>();
            }

            var connectionStringsResource = await response.Content.ReadFromJsonAsync<ConnectionStringsResource>();
            if (connectionStringsResource?.Properties == null)
            {
                return new List<ConnectionStringEntry>();
            }

            var result = connectionStringsResource.Properties.Select(kvp => new ConnectionStringEntry
            {
                Name = kvp.Key,
                Value = kvp.Value.Value,
                Type = kvp.Value.Type,
                Source = "App Service",
                IsSlotSetting = false,
                IsValueHidden = true
            }).OrderBy(c => c.Name).ToList();

            _logger.LogInformation("Retrieved {Count} connection strings", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch connection strings for {WebAppName}", webAppName);
            return new List<ConnectionStringEntry>();
        }
    }

    private static string DetermineSettingSource(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "App Service";

        // Check if it's a Key Vault reference
        if (value.StartsWith("@Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase))
            return "Key Vault Reference";

        return "App Service";
    }

    /// <summary>
    /// Saves environment variables (app settings) for a web app via the Azure API
    /// PUT /subscriptions/{subId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{siteName}/config/appsettings
    /// </summary>
    public async Task<bool> SaveEnvironmentVariablesAsync(string webAppName, List<EnvironmentVariable> environmentVariables)
    {
        try
        {
            var subscriptionId = _settings.SubscriptionId;
            var resourceGroup = _settings.ResourceGroup;

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
            {
                _logger.LogWarning("Subscription ID or Resource Group not configured");
                return false;
            }

            // Build the request body - Azure expects { "properties": { "key": "value", ... } }
            var requestBody = new
            {
                properties = environmentVariables.ToDictionary(e => e.Name, e => e.Value)
            };

            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}/config/appsettings");
            _logger.LogInformation("Saving app settings to: {Endpoint}", endpoint);

            var response = await _httpClient.PutAsJsonAsync(endpoint, requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to save app settings. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                return false;
            }

            _logger.LogInformation("Successfully saved {Count} app settings for {WebAppName}", environmentVariables.Count, webAppName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save environment variables for {WebAppName}", webAppName);
            return false;
        }
    }

    /// <summary>
    /// Saves connection strings for a web app via the Azure API
    /// PUT /subscriptions/{subId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{siteName}/config/connectionstrings
    /// </summary>
    public async Task<bool> SaveConnectionStringsAsync(string webAppName, List<ConnectionStringEntry> connectionStrings)
    {
        try
        {
            var subscriptionId = _settings.SubscriptionId;
            var resourceGroup = _settings.ResourceGroup;

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
            {
                _logger.LogWarning("Subscription ID or Resource Group not configured");
                return false;
            }

            // Build the request body - Azure expects { "properties": { "name": { "value": "...", "type": "..." }, ... } }
            var properties = connectionStrings.ToDictionary(
                c => c.Name,
                c => new { value = c.Value, type = c.Type }
            );
            var requestBody = new { properties };

            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}/config/connectionstrings");
            _logger.LogInformation("Saving connection strings to: {Endpoint}", endpoint);

            var response = await _httpClient.PutAsJsonAsync(endpoint, requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to save connection strings. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                return false;
            }

            _logger.LogInformation("Successfully saved {Count} connection strings for {WebAppName}", connectionStrings.Count, webAppName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save connection strings for {WebAppName}", webAppName);
            return false;
        }
    }

    public async Task<Dictionary<string, string>> GetAppSettingsAsync(string id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/webapps/{id}/settings");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Dictionary<string, string>>() 
                       ?? new Dictionary<string, string>();
            }
            return new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch app settings for {Id}", id);
            return new Dictionary<string, string>();
        }
    }

    public async Task<bool> UpdateAppSettingsAsync(string id, Dictionary<string, string> settings)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/webapps/{id}/settings", settings);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update app settings for {Id}", id);
            throw;
        }
    }

    #endregion

    #region Metrics

    public async Task<Dictionary<string, double>> GetMetricsAsync(string id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/webapps/{id}/metrics");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Dictionary<string, double>>() 
                       ?? new Dictionary<string, double>();
            }
            return new Dictionary<string, double>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch metrics for {Id}", id);
            return new Dictionary<string, double>();
        }
    }

    #endregion

    #region Identity Management

    /// <summary>
    /// Result object for identity assignment operations
    /// </summary>
    public class IdentityAssignmentResult
    {
        public bool Success { get; set; }
        public int? StatusCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RequestUrl { get; set; }
        public string? ResponseBody { get; set; }
    }

    /// <summary>
    /// Assigns a User Assigned Managed Identity to a web app via ARM PATCH
    /// PATCH /subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{name}?api-version=...
    /// </summary>
    public async Task<IdentityAssignmentResult> AssignUserIdentityAsync(string webAppName, string userAssignedIdentityResourceId)
    {
        var result = new IdentityAssignmentResult();

        try
        {
            var subscriptionId = _settings.SubscriptionId;
            var resourceGroup = _settings.ResourceGroup;

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
            {
                result.ErrorMessage = "Subscription ID or Resource Group not configured";
                _logger.LogWarning("Subscription ID or Resource Group not configured");
                return result;
            }

            if (string.IsNullOrEmpty(userAssignedIdentityResourceId))
            {
                result.ErrorMessage = "User Assigned Identity Resource ID is not configured";
                _logger.LogWarning("User Assigned Identity Resource ID is not configured");
                return result;
            }

            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}");

            result.RequestUrl = _httpClient.BaseAddress != null
                ? new Uri(_httpClient.BaseAddress, endpoint).ToString()
                : endpoint;

            // Build the PATCH body for assigning user assigned identity
            var requestBody = new
            {
                identity = new
                {
                    type = "UserAssigned",
                    userAssignedIdentities = new Dictionary<string, object>
                    {
                        { userAssignedIdentityResourceId, new { } }
                    }
                }
            };

            _logger.LogInformation("Assigning User Assigned Identity to {WebAppName}: {IdentityId}", webAppName, userAssignedIdentityResourceId);

            // Use PATCH method
            var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(requestBody)
            };

            using var response = await _httpClient.SendAsync(request);
            result.StatusCode = (int)response.StatusCode;
            result.ResponseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"ARM returned {response.StatusCode}";
                _logger.LogError("Failed to assign identity to {WebAppName}. Status: {StatusCode}, Response: {Response}",
                    webAppName, response.StatusCode, result.ResponseBody);
                return result;
            }

            result.Success = true;
            _logger.LogInformation("Successfully assigned User Assigned Identity to {WebAppName}", webAppName);
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Exception: {ex.Message}";
            _logger.LogError(ex, "Exception assigning identity to {WebAppName}", webAppName);
            return result;
        }
    }

    /// <summary>
    /// Removes the User Assigned Managed Identity from a web app via ARM PATCH
    /// PATCH /subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{name}?api-version=...
    /// </summary>
    public async Task<IdentityAssignmentResult> RemoveUserIdentityAsync(string webAppName, string userAssignedIdentityResourceId)
    {
        var result = new IdentityAssignmentResult();

        try
        {
            var subscriptionId = _settings.SubscriptionId;
            var resourceGroup = _settings.ResourceGroup;

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
            {
                result.ErrorMessage = "Subscription ID or Resource Group not configured";
                _logger.LogWarning("Subscription ID or Resource Group not configured");
                return result;
            }

            if (string.IsNullOrEmpty(userAssignedIdentityResourceId))
            {
                // If no identity configured, consider it a success (nothing to remove)
                result.Success = true;
                return result;
            }

            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}");

            result.RequestUrl = _httpClient.BaseAddress != null
                ? new Uri(_httpClient.BaseAddress, endpoint).ToString()
                : endpoint;

            // Build the PATCH body for removing user assigned identity (set to None)
            var requestBody = new
            {
                identity = new
                {
                    type = "None"
                }
            };

            _logger.LogInformation("Removing User Assigned Identity from {WebAppName}: {IdentityId}", webAppName, userAssignedIdentityResourceId);

            // Use PATCH method
            var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(requestBody)
            };

            using var response = await _httpClient.SendAsync(request);
            result.StatusCode = (int)response.StatusCode;
            result.ResponseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"ARM returned {response.StatusCode}";
                _logger.LogError("Failed to remove identity from {WebAppName}. Status: {StatusCode}, Response: {Response}",
                    webAppName, response.StatusCode, result.ResponseBody);
                return result;
            }

            result.Success = true;
            _logger.LogInformation("Successfully removed User Assigned Identity from {WebAppName}", webAppName);
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Exception: {ex.Message}";
            _logger.LogError(ex, "Exception removing identity from {WebAppName}", webAppName);
            return result;
        }
    }

    #endregion

    #region WebJobs

    /// <summary>
    /// Deletes a triggered webjob from a web app via ARM
    /// DELETE /subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{name}/triggeredwebjobs/{webjobName}
    /// </summary>
    public async Task<SiteExtensionResult> DeleteTriggeredWebJobAsync(string webAppName, string webjobName)
    {
        var result = new SiteExtensionResult { Method = "DELETE (ARM triggeredwebjobs API)" };

        try
        {
            var subscriptionId = _settings.SubscriptionId;
            var resourceGroup = _settings.ResourceGroup;

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
            {
                result.ErrorMessage = "Subscription ID or Resource Group not configured";
                _logger.LogWarning("Subscription ID or Resource Group not configured");
                return result;
            }

            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}/triggeredwebjobs/{webjobName}");

            result.RequestUrl = _httpClient.BaseAddress != null
                ? new Uri(_httpClient.BaseAddress, endpoint).ToString()
                : endpoint;

            _logger.LogInformation("Deleting triggered webjob {WebJobName} for {WebAppName} via ARM: {Endpoint}", webjobName, webAppName, endpoint);

            using var response = await _httpClient.DeleteAsync(endpoint);
            result.StatusCode = (int)response.StatusCode;
            result.ResponseBody = await response.Content.ReadAsStringAsync();

            // 404 is OK for delete - means it's already not there
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                result.ErrorMessage = $"ARM returned {response.StatusCode}";
                _logger.LogError("Failed to delete triggered webjob {WebJobName}. Status: {StatusCode}, Response: {Response}",
                    webjobName, response.StatusCode, result.ResponseBody);
                return result;
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Exception: {ex.Message}";
            _logger.LogError(ex, "Exception deleting triggered webjob {WebJobName} for {WebAppName}", webjobName, webAppName);
            return result;
        }
    }

    /// <summary>
    /// Deletes a continuous webjob from a web app via ARM
    /// DELETE /subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{name}/continuouswebjobs/{webjobName}
    /// </summary>
    public async Task<SiteExtensionResult> DeleteContinuousWebJobAsync(string webAppName, string webjobName)
    {
        var result = new SiteExtensionResult { Method = "DELETE (ARM continuouswebjobs API)" };

        try
        {
            var subscriptionId = _settings.SubscriptionId;
            var resourceGroup = _settings.ResourceGroup;

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
            {
                result.ErrorMessage = "Subscription ID or Resource Group not configured";
                _logger.LogWarning("Subscription ID or Resource Group not configured");
                return result;
            }

            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}/continuouswebjobs/{webjobName}");

            result.RequestUrl = _httpClient.BaseAddress != null
                ? new Uri(_httpClient.BaseAddress, endpoint).ToString()
                : endpoint;

            _logger.LogInformation("Deleting continuous webjob {WebJobName} for {WebAppName} via ARM: {Endpoint}", webjobName, webAppName, endpoint);

            using var response = await _httpClient.DeleteAsync(endpoint);
            result.StatusCode = (int)response.StatusCode;
            result.ResponseBody = await response.Content.ReadAsStringAsync();

            // 404 is OK for delete - means it's already not there
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                result.ErrorMessage = $"ARM returned {response.StatusCode}";
                _logger.LogError("Failed to delete continuous webjob {WebJobName}. Status: {StatusCode}, Response: {Response}",
                    webjobName, response.StatusCode, result.ResponseBody);
                return result;
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Exception: {ex.Message}";
            _logger.LogError(ex, "Exception deleting continuous webjob {WebJobName} for {WebAppName}", webjobName, webAppName);
            return result;
        }
    }

    #endregion

    #region Site Extensions

        private const string SiteExtensionsApiVersion = "2025-03-01";

        private string WithApiVersion(string endpoint, string apiVersion)
        {
            var separator = endpoint.Contains('?') ? '&' : '?';
            return $"{endpoint}{separator}api-version={apiVersion}";
        }

        private string SiteExtensionArmEndpoint(string webAppName, string siteExtensionId)
        {
            var subscriptionId = _settings.SubscriptionId;
            var resourceGroup = _settings.ResourceGroup;

            return $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}/siteextensions/{siteExtensionId}";
        }

        private bool HasArmSiteExtensionSettings(out string subscriptionId, out string resourceGroup)
        {
            subscriptionId = _settings.SubscriptionId;
            resourceGroup = _settings.ResourceGroup;

            if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
            {
                _logger.LogWarning("Subscription ID or Resource Group not configured");
                return false;
            }

            return true;
        }

        private async Task<SiteExtensionResult> PutArmSiteExtensionAsync(string webAppName, string siteExtensionId)
        {
            var result = new SiteExtensionResult { Method = "PUT (ARM siteextensions API)" };

            try
            {
                if (!HasArmSiteExtensionSettings(out _, out _))
                {
                    result.ErrorMessage = "Subscription ID or Resource Group not configured";
                    return result;
                }

                var endpoint = SiteExtensionArmEndpoint(webAppName, siteExtensionId);
                endpoint = WithApiVersion(endpoint, SiteExtensionsApiVersion);

                result.RequestUrl = _httpClient.BaseAddress != null
                    ? new Uri(_httpClient.BaseAddress, endpoint).ToString()
                    : endpoint;

                _logger.LogInformation("Installing site extension {SiteExtensionId} for {WebAppName} via ARM: {Endpoint}", siteExtensionId, webAppName, endpoint);

                // ARM expects a JSON body for PUT; an empty object works for this resource.
                using var response = await _httpClient.PutAsJsonAsync(endpoint, new { });
                result.StatusCode = (int)response.StatusCode;
                result.ResponseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"ARM returned {response.StatusCode}";
                    _logger.LogError("Failed to install site extension {SiteExtensionId}. Status: {StatusCode}, Response: {Response}",
                        siteExtensionId, response.StatusCode, result.ResponseBody);
                    return result;
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Exception: {ex.Message}";
                _logger.LogError(ex, "Exception installing site extension {SiteExtensionId} for {WebAppName}", siteExtensionId, webAppName);
                return result;
            }
        }

        private async Task<SiteExtensionResult> DeleteArmSiteExtensionAsync(string webAppName, string siteExtensionId)
        {
            var result = new SiteExtensionResult { Method = "DELETE (ARM siteextensions API)" };

            try
            {
                if (!HasArmSiteExtensionSettings(out _, out _))
                {
                    result.ErrorMessage = "Subscription ID or Resource Group not configured";
                    return result;
                }

                var endpoint = SiteExtensionArmEndpoint(webAppName, siteExtensionId);
                endpoint = WithApiVersion(endpoint, SiteExtensionsApiVersion);

                result.RequestUrl = _httpClient.BaseAddress != null
                    ? new Uri(_httpClient.BaseAddress, endpoint).ToString()
                    : endpoint;

                _logger.LogInformation("Uninstalling site extension {SiteExtensionId} for {WebAppName} via ARM: {Endpoint}", siteExtensionId, webAppName, endpoint);

                using var response = await _httpClient.DeleteAsync(endpoint);
                result.StatusCode = (int)response.StatusCode;
                result.ResponseBody = await response.Content.ReadAsStringAsync();

                // 404 is OK for uninstall - means it's already not there
                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                {
                    result.ErrorMessage = $"ARM returned {response.StatusCode}";
                    _logger.LogError("Failed to uninstall site extension {SiteExtensionId}. Status: {StatusCode}, Response: {Response}",
                        siteExtensionId, response.StatusCode, result.ResponseBody);
                    return result;
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Exception: {ex.Message}";
                _logger.LogError(ex, "Exception uninstalling site extension {SiteExtensionId} for {WebAppName}", siteExtensionId, webAppName);
                return result;
            }
        }

        private async Task<bool> IsArmSiteExtensionInstalledAsync(string webAppName, string siteExtensionId)
        {
            try
            {
                if (!HasArmSiteExtensionSettings(out _, out _))
                {
                    return false;
                }

                var endpoint = SiteExtensionArmEndpoint(webAppName, siteExtensionId);
                endpoint = WithApiVersion(endpoint, SiteExtensionsApiVersion);

                using var response = await _httpClient.GetAsync(endpoint);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check site extension {SiteExtensionId} status for {WebAppName}", siteExtensionId, webAppName);
                return false;
            }
        }

        /// <summary>
        /// Result object for site extension operations with detailed error info
        /// </summary>
        public class SiteExtensionResult
        {
            public bool Success { get; set; }
            public int? StatusCode { get; set; }
            public string? ErrorMessage { get; set; }
            public string? RequestUrl { get; set; }
            public string? ResponseBody { get; set; }
            public string? CredentialsUsed { get; set; }
            public string? Method { get; set; }
            public string? RawCredentials { get; set; }
        }

        /// <summary>
        /// Installs the EasyAgent site extension on a web app via ARM
        /// PUT /subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{name}/siteextensions/EasyAgent?api-version=2025-03-01
        /// </summary>
        public async Task<SiteExtensionResult> InstallEasyAgentExtensionAsync(string webAppName)
        {
            return await PutArmSiteExtensionAsync(webAppName, "EasyAgent");
        }

        /// <summary>
        /// Checks if the EasyAgent site extension is installed on a web app
        /// </summary>
        public async Task<bool> IsEasyAgentExtensionInstalledAsync(string webAppName)
        {
            return await IsArmSiteExtensionInstalledAsync(webAppName, "EasyAgent");
        }

        /// <summary>
        /// Uninstalls the EasyAgent site extension from a web app via the Kudu SCM API
        /// </summary>
        public async Task<SiteExtensionResult> UninstallEasyAgentExtensionAsync(string webAppName)
        {
            return await DeleteArmSiteExtensionAsync(webAppName, "EasyAgent");
        }

        /// <summary>
        /// Installs the EasyMCP site extension on a web app via the Kudu SCM API
        /// PUT https://{scm-url}/api/siteextensions/EasyMCP
        /// </summary>
        public async Task<SiteExtensionResult> InstallEasyMcpExtensionAsync(string webAppName)
        {
            return await PutArmSiteExtensionAsync(webAppName, "EasyMCP");
        }

        /// <summary>
        /// Checks if the EasyMCP site extension is installed on a web app
        /// </summary>
        public async Task<bool> IsEasyMcpExtensionInstalledAsync(string webAppName)
        {
            return await IsArmSiteExtensionInstalledAsync(webAppName, "EasyMCP");
        }

        /// <summary>
        /// Uninstalls the EasyMCP site extension from a web app via the Kudu SCM API
        /// </summary>
        public async Task<SiteExtensionResult> UninstallEasyMcpExtensionAsync(string webAppName)
        {
            return await DeleteArmSiteExtensionAsync(webAppName, "EasyMCP");
        }

        /// <summary>
        /// Installs the EasyAuth site extension on a web app via ARM
        /// </summary>
        public async Task<SiteExtensionResult> InstallEasyAuthExtensionAsync(string webAppName)
        {
            return await PutArmSiteExtensionAsync(webAppName, "EasyAuth");
        }

        /// <summary>
        /// Checks if the EasyAuth site extension is installed on a web app via ARM
        /// </summary>
        public async Task<bool> IsEasyAuthExtensionInstalledAsync(string webAppName)
        {
            return await IsArmSiteExtensionInstalledAsync(webAppName, "EasyAuth");
        }

        /// <summary>
        /// Uninstalls the EasyAuth site extension on a web app via ARM
        /// </summary>
        public async Task<SiteExtensionResult> UninstallEasyAuthExtensionAsync(string webAppName)
        {
            return await DeleteArmSiteExtensionAsync(webAppName, "EasyAuth");
        }

        #endregion

        #region Publishing Credentials

            /// <summary>
            /// Publishing credentials including SCM URL
            /// </summary>
            public class PublishingCredentials
            {
                public string UserName { get; set; } = string.Empty;
                public string Password { get; set; } = string.Empty;
                public string? ScmUrl { get; set; }
            }

            /// <summary>
            /// Retrieves the publishing credentials (username/password/scmUrl) from the publish profile XML
            /// POST /subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{siteName}/publishxml
            /// </summary>
            public async Task<PublishingCredentials?> GetPublishingCredentialsAsync(string webAppName)
            {
                try
                {
                    var subscriptionId = _settings.SubscriptionId;
                    var resourceGroup = _settings.ResourceGroup;

                    if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
                    {
                        _logger.LogWarning("Subscription ID or Resource Group not configured");
                        return null;
                    }

                    var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}/publishxml");
                    _logger.LogInformation("Fetching publish profile from: {Endpoint}", endpoint);

                    var response = await _httpClient.PostAsync(endpoint, null);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Failed to fetch publish profile. Status: {StatusCode}, Response: {Response}",
                            response.StatusCode, errorContent);
                        return null;
                    }

                    var xmlContent = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("Publish profile XML received: {Xml}", xmlContent);
            
                    var credentials = ParsePublishProfileCredentials(xmlContent);
            
                    // If no SCM URL from publish profile, try to get from site enabledHostNames
                    if (credentials != null && string.IsNullOrEmpty(credentials.ScmUrl))
                    {
                        credentials.ScmUrl = await GetScmUrlFromSiteAsync(webAppName);
                    }
            
                    return credentials;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch publishing credentials for {WebAppName}", webAppName);
                    return null;
                }
            }

            /// <summary>
            /// Gets the SCM URL from the site's enabledHostNames
            /// </summary>
            private async Task<string?> GetScmUrlFromSiteAsync(string webAppName)
            {
                try
                {
                    var subscriptionId = _settings.SubscriptionId;
                    var resourceGroup = _settings.ResourceGroup;

                    var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{webAppName}");
                    var response = await _httpClient.GetAsync(endpoint);

                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var webAppResource = await response.Content.ReadFromJsonAsync<WebAppResource>();
                    if (webAppResource?.Properties?.EnabledHostNames != null)
                    {
                        // Find the SCM hostname (contains ".scm.")
                        var scmHostName = webAppResource.Properties.EnabledHostNames
                            .FirstOrDefault(h => h.Contains(".scm.", StringComparison.OrdinalIgnoreCase));
                
                        if (!string.IsNullOrEmpty(scmHostName))
                        {
                            _logger.LogInformation("Found SCM URL from enabledHostNames: {ScmUrl}", scmHostName);
                            return $"https://{scmHostName}";
                        }
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get SCM URL from site for {WebAppName}", webAppName);
                    return null;
                }
            }

            /// <summary>
            /// Parses the publish profile XML to extract credentials and SCM URL
            /// </summary>
            private PublishingCredentials? ParsePublishProfileCredentials(string xmlContent)
            {
                try
                {
                    var doc = XDocument.Parse(xmlContent);

                    // Look for the MSDeploy publish profile which contains SCM credentials
                    var msDeployProfile = doc.Descendants("publishProfile")
                        .FirstOrDefault(p => p.Attribute("publishMethod")?.Value == "MSDeploy");

                    if (msDeployProfile == null)
                    {
                        // Fallback to any profile with userName and userPWD
                        msDeployProfile = doc.Descendants("publishProfile")
                            .FirstOrDefault(p => p.Attribute("userName") != null && p.Attribute("userPWD") != null);
                    }

                    if (msDeployProfile == null)
                    {
                        _logger.LogWarning("No valid publish profile found in XML");
                        return null;
                    }

                    var userName = msDeployProfile.Attribute("userName")?.Value;
                    var password = msDeployProfile.Attribute("userPWD")?.Value;
                    var publishUrl = msDeployProfile.Attribute("publishUrl")?.Value;

                    if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                    {
                        _logger.LogWarning("Username or password not found in publish profile");
                        return null;
                    }

                    // Extract SCM URL from publishUrl (remove :443 port if present)
                    string? scmUrl = null;
                    if (!string.IsNullOrEmpty(publishUrl))
                    {
                        // publishUrl is like: testsite-xxx.scm.amberwang2.eastus2-01.antares-test.windows-int.net:443
                        // We need: https://testsite-xxx.scm.amberwang2.eastus2-01.antares-test.windows-int.net
                        var cleanUrl = publishUrl.Split(':')[0]; // Remove port
                        scmUrl = $"https://{cleanUrl}";
                        _logger.LogInformation("Extracted SCM URL from publishUrl: {ScmUrl}", scmUrl);
                    }

                    _logger.LogInformation("Parsed credentials - User: {UserName}, Password length: {PasswordLength}, ScmUrl: {ScmUrl}", 
                        userName, password.Length, scmUrl ?? "N/A");
            
                    return new PublishingCredentials
                    {
                        UserName = userName,
                        Password = password,
                        ScmUrl = scmUrl
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse publish profile XML");
                    return null;
                }
            }

            /// <summary>
            /// Creates an HttpClient with Basic Auth configured for SCM site access
            /// </summary>
            private HttpClient CreateScmClient(string userName, string password)
            {
                var client = new HttpClient();
                // Use UTF8 encoding to handle special characters in passwords
                var credentialString = $"{userName}:{password}";
                var credentialBytes = Encoding.UTF8.GetBytes(credentialString);
                var base64Credentials = Convert.ToBase64String(credentialBytes);
        
                _logger.LogInformation("Creating SCM client - Credential string: '{CredString}' -> Base64: {Base64}", 
                    credentialString, base64Credentials);
        
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", base64Credentials);
                return client;
            }

            #endregion
        }
