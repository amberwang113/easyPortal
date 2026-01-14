namespace DevPortal.Models;

public class AppServicePlan
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Tier { get; set; } = "Basic";
    public string Size { get; set; } = "B1";
    public string OperatingSystem { get; set; } = "Windows";
    public int NumberOfWorkers { get; set; } = 1;
    public PricingTier PricingTier { get; set; } = new();
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public class PricingTier
{
    public string Name { get; set; } = "Basic";
    public string Sku { get; set; } = "B1";
    public int Cores { get; set; } = 1;
    public double RamGB { get; set; } = 1.75;
    public int StorageGB { get; set; } = 10;
    public decimal PricePerHour { get; set; } = 0.075m;
}
