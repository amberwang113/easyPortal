using System.Security.Cryptography.X509Certificates;
using DevPortal.Models;
using DevPortal.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure API settings
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("Api"));

// Configure EasyAgent default settings
builder.Services.Configure<EasyAgentSettings>(builder.Configuration.GetSection("EasyAgent"));

// Configure HttpClient for ApiService
builder.Services.AddHttpClient<ApiService>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["Api:BaseUrl"];
    
    if (!string.IsNullOrEmpty(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
    
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("HttpClientCertificateHandler");
    var authType = configuration["Api:AuthType"] ?? "None";
    
    var handler = new HttpClientHandler();
    
    // Configure certificate authentication if specified
    if (authType.Equals("Certificate", StringComparison.OrdinalIgnoreCase))
    {
        var thumbprint = configuration["Api:Certificate:Thumbprint"];
        var storeName = configuration["Api:Certificate:StoreName"] ?? "My";
        var storeLocation = configuration["Api:Certificate:StoreLocation"] ?? "CurrentUser";
        
        logger.LogInformation("Certificate auth configured. Thumbprint: {Thumbprint}, Store: {StoreName}/{StoreLocation}", 
            thumbprint, storeName, storeLocation);
        
        if (!string.IsNullOrEmpty(thumbprint))
        {
            var store = new X509Store(
                Enum.Parse<StoreName>(storeName),
                Enum.Parse<StoreLocation>(storeLocation));
            
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            store.Close();
            
            if (certs.Count > 0)
            {
                var cert = certs[0];
                handler.ClientCertificates.Add(cert);
                logger.LogInformation(
                    "Client certificate loaded: Subject={Subject}, Issuer={Issuer}, Thumbprint={Thumbprint}, HasPrivateKey={HasPrivateKey}, NotBefore={NotBefore}, NotAfter={NotAfter}", 
                    cert.Subject, 
                    cert.Issuer,
                    cert.Thumbprint,
                    cert.HasPrivateKey,
                    cert.NotBefore,
                    cert.NotAfter);
                
                // Warn if certificate is expired or not yet valid
                if (DateTime.Now < cert.NotBefore)
                {
                    logger.LogWarning("Certificate is not yet valid! NotBefore: {NotBefore}", cert.NotBefore);
                }
                if (DateTime.Now > cert.NotAfter)
                {
                    logger.LogWarning("Certificate has EXPIRED! NotAfter: {NotAfter}", cert.NotAfter);
                }
                if (!cert.HasPrivateKey)
                {
                    logger.LogError("Certificate does NOT have a private key - TLS client auth will FAIL!");
                }
            }
            else
            {
                logger.LogError("Certificate with thumbprint {Thumbprint} not found in {StoreLocation}/{StoreName}. " +
                    "Make sure the certificate is installed in the correct store and the thumbprint is correct (no spaces, uppercase).", 
                    thumbprint, storeLocation, storeName);
            }
        }
        else
        {
            logger.LogWarning("Certificate thumbprint is not configured");
        }
        
        // Ensure the client certificate is sent automatically during TLS handshake
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
    }
    
    // For development, you might need to bypass SSL validation
    // Remove this in production!
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    
    return handler;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
