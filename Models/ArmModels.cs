using System.Text.Json.Serialization;

namespace DevPortal.Models;

/// <summary>
/// Response model for API list operations
/// </summary>
public class ListResponse<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();
    
    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }
}

/// <summary>
/// API response model for a web app (Microsoft.Web/sites)
/// </summary>
public class WebAppResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;
    
    [JsonPropertyName("properties")]
    public WebAppResourceProperties Properties { get; set; } = new();
    
    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}

public class WebAppResourceProperties
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
    
    [JsonPropertyName("hostNames")]
    public List<string> HostNames { get; set; } = new();
    
    [JsonPropertyName("defaultHostName")]
    public string DefaultHostName { get; set; } = string.Empty;
    
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
    
    [JsonPropertyName("httpsOnly")]
    public bool HttpsOnly { get; set; }
    
    [JsonPropertyName("serverFarmId")]
    public string ServerFarmId { get; set; } = string.Empty;
    
    [JsonPropertyName("resourceGroup")]
    public string ResourceGroup { get; set; } = string.Empty;
    
    [JsonPropertyName("lastModifiedTimeUtc")]
    public DateTime? LastModifiedTimeUtc { get; set; }
    
    [JsonPropertyName("usageState")]
    public string UsageState { get; set; } = string.Empty;
    
    [JsonPropertyName("availabilityState")]
    public string AvailabilityState { get; set; } = string.Empty;
    
    [JsonPropertyName("siteConfig")]
    public SiteConfig? SiteConfig { get; set; }
    
    [JsonPropertyName("sku")]
    public string? Sku { get; set; }
    
    [JsonPropertyName("reserved")]
    public bool Reserved { get; set; }
    
    [JsonPropertyName("hyperV")]
    public bool HyperV { get; set; }
    
    [JsonPropertyName("clientAffinityEnabled")]
    public bool ClientAffinityEnabled { get; set; }
    
    [JsonPropertyName("inboundIpAddress")]
    public string? InboundIpAddress { get; set; }
    
    [JsonPropertyName("outboundIpAddresses")]
    public string? OutboundIpAddresses { get; set; }
    
    [JsonPropertyName("publicNetworkAccess")]
    public string? PublicNetworkAccess { get; set; }
}

public class SiteConfig
{
    [JsonPropertyName("numberOfWorkers")]
    public int NumberOfWorkers { get; set; }
    
    [JsonPropertyName("linuxFxVersion")]
    public string? LinuxFxVersion { get; set; }
    
    [JsonPropertyName("windowsFxVersion")]
    public string? WindowsFxVersion { get; set; }
    
    [JsonPropertyName("netFrameworkVersion")]
    public string? NetFrameworkVersion { get; set; }
    
    [JsonPropertyName("alwaysOn")]
    public bool AlwaysOn { get; set; }
    
    [JsonPropertyName("http20Enabled")]
    public bool Http20Enabled { get; set; }
    
    [JsonPropertyName("webSocketsEnabled")]
    public bool? WebSocketsEnabled { get; set; }
    
    [JsonPropertyName("javaVersion")]
    public string? JavaVersion { get; set; }
    
    [JsonPropertyName("pythonVersion")]
    public string? PythonVersion { get; set; }
    
    [JsonPropertyName("nodeVersion")]
    public string? NodeVersion { get; set; }
    
    [JsonPropertyName("phpVersion")]
    public string? PhpVersion { get; set; }
    
    [JsonPropertyName("ftpsState")]
    public string? FtpsState { get; set; }
    
    [JsonPropertyName("minTlsVersion")]
    public string? MinTlsVersion { get; set; }
    
    [JsonPropertyName("healthCheckPath")]
    public string? HealthCheckPath { get; set; }
    
    [JsonPropertyName("use32BitWorkerProcess")]
    public bool? Use32BitWorkerProcess { get; set; }
}

/// <summary>
/// Response model for App Settings API (Microsoft.Web/sites/config/appsettings)
/// </summary>
public class AppSettingsResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("properties")]
    public Dictionary<string, string> Properties { get; set; } = new();
}

/// <summary>
/// Response model for Connection Strings API (Microsoft.Web/sites/config/connectionstrings)
/// </summary>
public class ConnectionStringsResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("properties")]
    public Dictionary<string, ConnectionStringValue> Properties { get; set; } = new();
}

public class ConnectionStringValue
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // SQLServer, SQLAzure, MySql, Custom, etc.
}

/// <summary>
/// Represents an environment variable (app setting) with metadata
/// </summary>
public class EnvironmentVariable
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Source { get; set; } = "App Service"; // App Service, Slot Setting, Key Vault Reference
    public bool IsSlotSetting { get; set; } = false;
    public bool IsValueHidden { get; set; } = true;
}

/// <summary>
/// Represents a connection string with metadata
/// </summary>
public class ConnectionStringEntry
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = "Custom"; // SQLServer, SQLAzure, MySql, PostgreSQL, Custom
    public string Source { get; set; } = "App Service";
    public bool IsSlotSetting { get; set; } = false;
    public bool IsValueHidden { get; set; } = true;
}
