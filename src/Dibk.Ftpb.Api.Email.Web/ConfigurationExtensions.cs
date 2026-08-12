using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dibk.Ftpb.Email.Api;

/// <summary>
/// Loads application settings from the shared Azure App Configuration store
/// </summary>
public static class ConfigurationExtensions
{

    /// <summary>Key prefix holding this application's own values.</summary>
    private const string EmailApiPrefix = "EMAIL/";

    /// <summary>
    /// Adds App Configuration as a configuration source.
    /// </summary>
    public static IConfigurationBuilder AddEmailApiConfiguration(
        this IConfigurationManager configuration,
        string environmentName)
    {
        var appConfigurationUri = configuration["AppConfiguration_Uri"];

        if (string.IsNullOrWhiteSpace(appConfigurationUri))
        {
            Console.WriteLine("AppConfiguration_Uri is not configured; skipping App Configuration as a config provider.");
            return configuration;
        }

        Console.WriteLine($"Loading configuration from {appConfigurationUri} with label '{environmentName}'.");

        var credential = GetAzureCredential(configuration["Azure:TenantId"]);

        configuration.AddAzureAppConfiguration(options =>
        {
            options.Connect(new Uri(appConfigurationUri), credential)
                   .Select(KeyFilter.Any, LabelFilter.Null)
                   .Select(KeyFilter.Any, environmentName)
                   .TrimKeyPrefix(EmailApiPrefix)
                   .ConfigureKeyVault(kv => kv.SetCredential(credential));
        });

        // Loaded once at startup; there is no refresh, so a configuration change needs a restart.
        return configuration.AddEnvironmentVariables();
    }

    private static DefaultAzureCredential GetAzureCredential(string tenantId) =>
        string.IsNullOrWhiteSpace(tenantId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = tenantId });
}
