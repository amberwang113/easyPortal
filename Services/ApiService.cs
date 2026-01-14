using DevPortal.Models;

namespace DevPortal.Services;

public class ApiService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private List<WebApp> _mockWebApps;

    public ApiService(IConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient();
        
        // Initialize mock data for demonstration
        InitializeMockData();
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

    // TODO: Replace with actual API calls to your backend
    public async Task<List<WebApp>> GetWebAppsAsync()
    {
        // Simulate API delay
        await Task.Delay(500);
        
        // TODO: Uncomment and implement when backend is ready
        // var response = await _httpClient.GetAsync($"{GetBaseUrl()}/api/webapps");
        // response.EnsureSuccessStatusCode();
        // return await response.Content.ReadFromJsonAsync<List<WebApp>>() ?? new();
        
        return _mockWebApps;
    }

    public async Task<WebApp?> GetWebAppAsync(string id)
    {
        // Simulate API delay
        await Task.Delay(300);
        
        // TODO: Uncomment and implement when backend is ready
        // var response = await _httpClient.GetAsync($"{GetBaseUrl()}/api/webapps/{id}");
        // if (!response.IsSuccessStatusCode) return null;
        // return await response.Content.ReadFromJsonAsync<WebApp>();
        
        return _mockWebApps.FirstOrDefault(w => w.Id == id);
    }

    public async Task<WebApp> CreateWebAppAsync(WebApp webApp)
    {
        // Simulate API delay
        await Task.Delay(1000);
        
        // TODO: Uncomment and implement when backend is ready
        // var response = await _httpClient.PostAsJsonAsync($"{GetBaseUrl()}/api/webapps", webApp);
        // response.EnsureSuccessStatusCode();
        // return await response.Content.ReadFromJsonAsync<WebApp>() ?? webApp;
        
        webApp.Id = $"webapp-{Guid.NewGuid().ToString("N")[..8]}";
        webApp.CreatedDate = DateTime.UtcNow;
        webApp.LastModifiedDate = DateTime.UtcNow;
        _mockWebApps.Add(webApp);
        return webApp;
    }

    public async Task<WebApp> UpdateWebAppAsync(string id, WebApp webApp)
    {
        // Simulate API delay
        await Task.Delay(800);
        
        // TODO: Uncomment and implement when backend is ready
        // var response = await _httpClient.PutAsJsonAsync($"{GetBaseUrl()}/api/webapps/{id}", webApp);
        // response.EnsureSuccessStatusCode();
        // return await response.Content.ReadFromJsonAsync<WebApp>() ?? webApp;
        
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

    public async Task<bool> DeleteWebAppAsync(string id)
    {
        // Simulate API delay
        await Task.Delay(1000);
        
        // TODO: Uncomment and implement when backend is ready
        // var response = await _httpClient.DeleteAsync($"{GetBaseUrl()}/api/webapps/{id}");
        // return response.IsSuccessStatusCode;
        
        var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
        if (webApp != null)
        {
            _mockWebApps.Remove(webApp);
            return true;
        }
        return false;
    }

    public async Task<bool> StartWebAppAsync(string id)
    {
        // Simulate API delay
        await Task.Delay(2000);
        
        // TODO: Uncomment and implement when backend is ready
        // var response = await _httpClient.PostAsync($"{GetBaseUrl()}/api/webapps/{id}/start", null);
        // return response.IsSuccessStatusCode;
        
        var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
        if (webApp != null)
        {
            webApp.Status = "Running";
            webApp.LastModifiedDate = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    public async Task<bool> StopWebAppAsync(string id)
    {
        // Simulate API delay
        await Task.Delay(2000);
        
        // TODO: Uncomment and implement when backend is ready
        // var response = await _httpClient.PostAsync($"{GetBaseUrl()}/api/webapps/{id}/stop", null);
        // return response.IsSuccessStatusCode;
        
        var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
        if (webApp != null)
        {
            webApp.Status = "Stopped";
            webApp.LastModifiedDate = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    public async Task<bool> RestartWebAppAsync(string id)
    {
        // Simulate API delay
        await Task.Delay(3000);
        
        // TODO: Uncomment and implement when backend is ready
        // var response = await _httpClient.PostAsync($"{GetBaseUrl()}/api/webapps/{id}/restart", null);
        // return response.IsSuccessStatusCode;
        
        var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
        if (webApp != null)
        {
            webApp.LastModifiedDate = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    #endregion

    #region Configuration

    public async Task<Dictionary<string, string>> GetAppSettingsAsync(string id)
    {
        await Task.Delay(300);
        
        // TODO: Implement actual API call
        var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
        return webApp?.AppSettings ?? new Dictionary<string, string>();
    }

    public async Task<bool> UpdateAppSettingsAsync(string id, Dictionary<string, string> settings)
    {
        await Task.Delay(800);
        
        // TODO: Implement actual API call
        var webApp = _mockWebApps.FirstOrDefault(w => w.Id == id);
        if (webApp != null)
        {
            webApp.AppSettings = settings;
            webApp.LastModifiedDate = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    #endregion

    #region Metrics

    public async Task<Dictionary<string, double>> GetMetricsAsync(string id)
    {
        await Task.Delay(500);
        
        // TODO: Implement actual API call
        // Return mock metrics
        return new Dictionary<string, double>
        {
            { "CPU", Random.Shared.NextDouble() * 100 },
            { "Memory", Random.Shared.NextDouble() * 100 },
            { "Requests", Random.Shared.Next(1000, 10000) },
            { "ResponseTime", Random.Shared.NextDouble() * 1000 }
        };
    }

    #endregion

    private string GetBaseUrl()
    {
        // TODO: Get from configuration
        return _configuration["ApiBaseUrl"] ?? "https://your-backend-api.com";
    }
}
