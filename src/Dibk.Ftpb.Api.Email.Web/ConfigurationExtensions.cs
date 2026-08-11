using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Dibk.Ftpb.Email.Api;

/// <summary>
/// Loads application settings from the shared Azure App Configuration store, the same store
/// <c>ftpb-core-functions</c> reads. Shared values (Elastic, APM) are defined once there rather than
/// duplicated per project.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>Key prefix holding values shared by all FTPB applications.</summary>
    private const string SharedPrefix = "CF/";

    /// <summary>Key prefix holding this application's own values.</summary>
    private const string EmailApiPrefix = "EMAIL/";

    /// <summary>
    /// Adds App Configuration as a configuration source, then re-adds environment variables so that
    /// App Service app settings still win. If <c>AppConfiguration_Uri</c> is not set — local
    /// development, or a deployment that declares its settings directly — this is a no-op and the
    /// app runs entirely on app settings.
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
            // Only the two prefixes this app needs — unlike core-functions, which selects the whole
            // store and therefore loads every other project's keys.
            // Unlabelled values first, then environment-labelled ones, which override them.
            options.Connect(new Uri(appConfigurationUri), credential)
                   .Select($"{SharedPrefix}*", LabelFilter.Null)
                   .Select($"{SharedPrefix}*", environmentName)
                   .Select($"{EmailApiPrefix}*", LabelFilter.Null)
                   .Select($"{EmailApiPrefix}*", environmentName)
                   .TrimKeyPrefix(SharedPrefix)
                   .TrimKeyPrefix(EmailApiPrefix)
                   // Values stored as Key Vault references are resolved with the same identity.
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
