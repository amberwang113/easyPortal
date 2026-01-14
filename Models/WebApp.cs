namespace DevPortal.Models;

public class WebApp
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = "Running";
    public string Url { get; set; } = string.Empty;
    public string Runtime { get; set; } = string.Empty;
    public AppServicePlan? AppServicePlan { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
    
    // Configuration
    public Dictionary<string, string> AppSettings { get; set; } = new();
    public Dictionary<string, string> ConnectionStrings { get; set; } = new();
    public bool AlwaysOn { get; set; } = false;
    public bool HttpsOnly { get; set; } = true;
    public string HttpVersion { get; set; } = "2.0";
    public bool WebSocketsEnabled { get; set; } = false;
    
    // Deployment
    public string DeploymentMethod { get; set; } = "None";
    public string RepositoryUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public DateTime? LastDeploymentDate { get; set; }
    public string LastDeploymentStatus { get; set; } = string.Empty;
    
    // Networking
    public bool VNetIntegrationEnabled { get; set; } = false;
    public string VNetName { get; set; } = string.Empty;
    public string SubnetName { get; set; } = string.Empty;
    public List<string> AllowedIpAddresses { get; set; } = new();
    public bool PrivateEndpointEnabled { get; set; } = false;
    
    // Monitoring
    public bool ApplicationInsightsEnabled { get; set; } = false;
    public string ApplicationInsightsKey { get; set; } = string.Empty;
    public Dictionary<string, double> Metrics { get; set; } = new();
    
    // Scaling
    public bool AutoScaleEnabled { get; set; } = false;
    public int MinInstances { get; set; } = 1;
    public int MaxInstances { get; set; } = 1;
    public int CurrentInstances { get; set; } = 1;
    public List<ScaleRule> ScaleRules { get; set; } = new();
}

public class ScaleRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MetricName { get; set; } = string.Empty;
    public string Operator { get; set; } = "GreaterThan";
    public double Threshold { get; set; }
    public int Duration { get; set; } = 5;
    public string Action { get; set; } = "Increase";
    public int InstanceCount { get; set; } = 1;
}
