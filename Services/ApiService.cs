using System.Net.Http.Json;
using DevPortal.Models;
using Microsoft.Extensions.Options;

namespace DevPortal.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ApiSettings _settings;
    private readonly ILogger<ApiService> _logger;
    private readonly bool _useMockData;
    private List<WebApp> _mockWebApps;

    public ApiService(HttpClient httpClient, IOptions<ApiSettings> settings, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        
        // Use mock data if no base URL is configured or if it's the placeholder
        _useMockData = string.IsNullOrEmpty(_settings.BaseUrl) || 
                       _settings.BaseUrl.Contains("your-backend-api");
        
        _mockWebApps = new List<WebApp>();
        InitializeMockData();
    }

    /// <summary>
    /// Appends the API version query parameter to an endpoint
    /// </summary>
    private string WithApiVersion(string endpoint)
    {
        var separator = endpoint.Contains('?') ? '&' : '?';
        return $"{endpoint}{separator}api-version={_settings.ApiVersion}";
    }

    private void InitializeMockData()
    {
        _mockWebApps = new List<WebApp>
        {
            new WebApp
            {
                Id = "webapp-001",
                Name = "my-production-app",
                ResourceGroup = "production-rg",
                Location = "East US",
                Status = "Running",
                Url = "https://my-production-app.azurewebsites.net",
                Runtime = ".NET 8.0",
                AppServicePlan = new AppServicePlan
                {
                    Id = "asp-001",
                    Name = "production-asp",
                    Tier = "Premium",
                    Size = "P1v2",
                    Location = "East US"
                },
                CreatedDate = DateTime.UtcNow.AddMonths(-6),
                LastModifiedDate = DateTime.UtcNow.AddDays(-2),
                AlwaysOn = true,
                HttpsOnly = true,
                ApplicationInsightsEnabled = true,
                CurrentInstances = 2
            },
            new WebApp
            {
                Id = "webapp-002",
                Name = "staging-api",
                ResourceGroup = "staging-rg",
                Location = "West US",
                Status = "Running",
                Url = "https://staging-api.azurewebsites.net",
                Runtime = ".NET 8.0",
                AppServicePlan = new AppServicePlan
                {
                    Id = "asp-002",
                    Name = "staging-asp",
                    Tier = "Standard",
                    Size = "S1",
                    Location = "West US"
                },
                CreatedDate = DateTime.UtcNow.AddMonths(-3),
                LastModifiedDate = DateTime.UtcNow.AddHours(-5),
                AlwaysOn = true,
                HttpsOnly = true,
                CurrentInstances = 1
            },
            new WebApp
            {
                Id = "webapp-003",
                Name = "dev-test-app",
                ResourceGroup = "development-rg",
                Location = "Central US",
                Status = "Stopped",
                Url = "https://dev-test-app.azurewebsites.net",
                Runtime = ".NET 8.0",
                AppServicePlan = new AppServicePlan
                {
                    Id = "asp-003",
                    Name = "dev-asp",
                    Tier = "Basic",
                    Size = "B1",
                    Location = "Central US"
                },
                CreatedDate = DateTime.UtcNow.AddMonths(-1),
                LastModifiedDate = DateTime.UtcNow.AddDays(-10),
                AlwaysOn = false,
                HttpsOnly = true,
                CurrentInstances = 1
            }
        };
    }

    #region Web Apps

    public async Task<List<WebApp>> GetWebAppsAsync()
    {
        // Always start with mock data so test websites remain clickable
        var allWebApps = new List<WebApp>(_mockWebApps);
        
        // If using mock data only, return just the mock apps
        if (_useMockData)
        {
            await Task.Delay(500);
            return allWebApps;
        }

        // Try to fetch real web apps from the API and merge them
        try
        {
            var subscriptionId = _settings.SubscriptionId;
            if (string.IsNullOrEmpty(subscriptionId))
            {
                _logger.LogWarning("No subscription ID configured, returning mock data only");
                return allWebApps;
            }

            var resourceGroup = _settings.ResourceGroup;
            if (string.IsNullOrEmpty(resourceGroup))
            {
                _logger.LogWarning("No resource group configured, returning mock data only");
                return allWebApps;
            }

            var endpoint = WithApiVersion($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites");
            _logger.LogInformation("Fetching web apps from API: {BaseAddress}{Endpoint}", _httpClient.BaseAddress, endpoint);
            
            var response = await _httpClient.GetAsync(endpoint);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("API request failed with status {StatusCode}: {ReasonPhrase}. Response: {ErrorContent}", 
                    (int)response.StatusCode, response.ReasonPhrase, errorContent);
            }
            
            response.EnsureSuccessStatusCode();
            
            var listResponse = await response.Content.ReadFromJsonAsync<ListResponse<WebAppResource>>();
            if (listResponse?.Value != null && listResponse.Value.Count > 0)
            {
                var realWebApps = listResponse.Value.Select(MapWebAppResourceToWebApp).ToList();
                _logger.LogInformation("Retrieved {Count} web apps from API", realWebApps.Count);
                
                // Add real web apps to the list (after mock data)
                allWebApps.AddRange(realWebApps);
            }
            else
            {
                _logger.LogWarning("Empty response from API, returning mock data only");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch web apps from API, returning mock data only");
        }

        return allWebApps;
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
        // First check if it's a mock web app (so test websites remain clickable)
        var mockWebApp = _mockWebApps.FirstOrDefault(w => w.Id == id || w.Name == id);
        if (mockWebApp != null)
        {
            await Task.Delay(300);
            return mockWebApp;
        }
        
        // If not mock data only mode, try to fetch from API
        if (!_useMockData)
        {
            try
            {
                _logger.LogInformation("Fetching web app {Id} from API", id);
                
                // Build the ARM resource ID from the web app name
                var subscriptionId = _settings.SubscriptionId;
                var resourceGroup = _settings.ResourceGroup;
                
                if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup))
                {
                    _logger.LogWarning("Subscription ID or Resource Group not configured");
                    return null;
                }
                
                // Construct the endpoint using the web app name
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch web app {Id} from API", id);
            }
        }

        return null;
    }

    public async Task<WebApp> CreateWebAppAsync(WebApp webApp)
    {
        if (_useMockData)
        {
            await Task.Delay(1000);
            webApp.Id = $"webapp-{Guid.NewGuid().ToString("N")[..8]}";
            webApp.CreatedDate = DateTime.UtcNow;
            webApp.LastModifiedDate = DateTime.UtcNow;
            _mockWebApps.Add(webApp);
            return webApp;
        }

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
        if (_useMockData)
        {
            await Task.Delay(800);
            var existing = _mockWebApps.FirstOrDefault(w => w.Id == id);
            if (existing != null)
            {
                var index = _mockWebApps.IndexOf(existing);
                webApp.Id = id;
                webApp.LastModifiedDate = DateTime.UtcNow;
                _mockWebApps[index] = webApp;
            }
            return webApp;
        }

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
        if (_useMockData)
        {
            await Task.Delay(1000);
            var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
            if (webApp != null)
            {
                _mockWebApps.Remove(webApp);
                return true;
            }
            return false;
        }

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
        if (_useMockData)
        {
            await Task.Delay(2000);
            var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
            if (webApp != null)
            {
                webApp.Status = "Running";
                webApp.LastModifiedDate = DateTime.UtcNow;
                return true;
            }
            return false;
        }

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
        if (_useMockData)
        {
            await Task.Delay(2000);
            var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
            if (webApp != null)
            {
                webApp.Status = "Stopped";
                webApp.LastModifiedDate = DateTime.UtcNow;
                return true;
            }
            return false;
        }

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
        if (_useMockData)
        {
            await Task.Delay(3000);
            var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
            if (webApp != null)
            {
                webApp.LastModifiedDate = DateTime.UtcNow;
                return true;
            }
            return false;
        }

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
        // Check for mock web app first
        var mockWebApp = _mockWebApps.FirstOrDefault(w => w.Id == webAppName || w.Name == webAppName);
        if (mockWebApp != null)
        {
            await Task.Delay(300);
            return mockWebApp.AppSettings.Select(kvp => new EnvironmentVariable
            {
                Name = kvp.Key,
                Value = kvp.Value,
                Source = "App Service",
                IsSlotSetting = false,
                IsValueHidden = true
            }).ToList();
        }

        if (_useMockData)
        {
            await Task.Delay(300);
            return new List<EnvironmentVariable>();
        }

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
                IsSlotSetting = false, // Would need separate API call to get slot settings
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
        // Check for mock web app first
        var mockWebApp = _mockWebApps.FirstOrDefault(w => w.Id == webAppName || w.Name == webAppName);
        if (mockWebApp != null)
        {
            await Task.Delay(300);
            return mockWebApp.ConnectionStrings.Select(kvp => new ConnectionStringEntry
            {
                Name = kvp.Key,
                Value = kvp.Value,
                Type = "Custom",
                Source = "App Service",
                IsSlotSetting = false,
                IsValueHidden = true
            }).ToList();
        }

        if (_useMockData)
        {
            await Task.Delay(300);
            return new List<ConnectionStringEntry>();
        }

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
        // Check for mock web app first
        var mockWebApp = _mockWebApps.FirstOrDefault(w => w.Id == webAppName || w.Name == webAppName);
        if (mockWebApp != null)
        {
            await Task.Delay(500);
            mockWebApp.AppSettings = environmentVariables.ToDictionary(e => e.Name, e => e.Value);
            mockWebApp.LastModifiedDate = DateTime.UtcNow;
            _logger.LogInformation("Saved {Count} app settings to mock web app {Name}", environmentVariables.Count, webAppName);
            return true;
        }

        if (_useMockData)
        {
            await Task.Delay(500);
            return true;
        }

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
        // Check for mock web app first
        var mockWebApp = _mockWebApps.FirstOrDefault(w => w.Id == webAppName || w.Name == webAppName);
        if (mockWebApp != null)
        {
            await Task.Delay(500);
            mockWebApp.ConnectionStrings = connectionStrings.ToDictionary(c => c.Name, c => c.Value);
            mockWebApp.LastModifiedDate = DateTime.UtcNow;
            _logger.LogInformation("Saved {Count} connection strings to mock web app {Name}", connectionStrings.Count, webAppName);
            return true;
        }

        if (_useMockData)
        {
            await Task.Delay(500);
            return true;
        }

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
        if (_useMockData)
        {
            await Task.Delay(300);
            var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
            return webApp?.AppSettings ?? new Dictionary<string, string>();
        }

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
        if (_useMockData)
        {
            await Task.Delay(800);
            var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
            if (webApp != null)
            {
                webApp.AppSettings = settings;
                webApp.LastModifiedDate = DateTime.UtcNow;
                return true;
            }
            return false;
        }

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
        if (_useMockData)
        {
            await Task.Delay(500);
            return new Dictionary<string, double>
            {
                { "CPU", Random.Shared.NextDouble() * 100 },
                { "Memory", Random.Shared.NextDouble() * 100 },
                { "Requests", Random.Shared.Next(1000, 10000) },
                { "ResponseTime", Random.Shared.NextDouble() * 1000 }
            };
        }

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
}
