using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using static Dibk.Ftpb.Api.Email.Provider.GraphApi.GraphApiEmailProvider;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi;

public static class ServiceConfigurationExtension
{
    /// <summary>Tag for checks that belong on the readiness endpoint, not the liveness one.</summary>
    public const string ReadyTag = "ready";

    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

    public static void AddGraphApiEmailProvider(this IServiceCollection services, IConfiguration configuration)
    {
        // Authenticate to Microsoft Graph with the client-credentials flow using a certificate.
        // The certificate is loaded from Key Vault via the app's managed identity (DefaultAzureCredential).
        // Registered as a singleton so the Key Vault download happens once, on first use.
        services.AddSingleton<TokenCredential>(_ =>
        {
            var tenantId = GetRequiredConfig(configuration, "GraphApiAuth:TenantId");
            var clientId = GetRequiredConfig(configuration, "GraphApiAuth:ClientId");
            var keyVaultUri = GetRequiredConfig(configuration, "GraphApiAuth:KeyVaultUri");
            var certificateName = GetRequiredConfig(configuration, "GraphApiAuth:CertificateName");

            var certificateClient = new CertificateClient(new Uri(keyVaultUri), new DefaultAzureCredential());
            X509Certificate2 certificate = certificateClient.DownloadCertificate(certificateName);

            return new ClientCertificateCredential(tenantId, clientId, certificate);
        });

        // GraphServiceClient is thread-safe and meant to be reused, so register it as a singleton.
        services.AddSingleton(sp => new GraphServiceClient(sp.GetRequiredService<TokenCredential>(), GraphScopes));

        services.AddScoped<Interfaces.IFtpbEmailProvider, GraphApiEmailProvider>();
        services.Configure<GraphApiEmailSettings>(configuration.GetSection(GraphApiEmailSettings.ConfigSection));
    }

    /// <summary>
    /// Registers the Graph connectivity check under the <c>ready</c> tag. It is deliberately kept out
    /// of the liveness endpoint: App Service recycles instances on sustained health-check failure, so
    /// a Key Vault blip or an Entra outage would otherwise restart healthy instances.
    /// </summary>
    public static IHealthChecksBuilder AddGraphApiHealthCheck(this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<GraphApiHealthCheck>("GraphApi connection check", tags: [ReadyTag]);
    }

    private static string GetRequiredConfig(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing configuration '{key}'.");
        return value;
    }
}
