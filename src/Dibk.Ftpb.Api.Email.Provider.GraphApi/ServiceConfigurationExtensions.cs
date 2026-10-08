using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using static Dibk.Ftpb.Api.Email.Provider.GraphApi.GraphApiEmailProvider;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi;

public static class ServiceConfigurationExtension
{
    public const string ReadyTag = "ready";

    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

    /// <summary>Audience Entra requires on a managed-identity token used as a client assertion.</summary>
    private const string TokenExchangeAudience = "api://AzureADTokenExchange";

    public static void AddGraphApiEmailProvider(this IServiceCollection services, IConfiguration configuration)
    {
        // Registered as a singleton because both credentials cache tokens internally.
        services.AddSingleton<TokenCredential>(sp =>
        {
            var tenantId = GetRequiredConfig(configuration, "GraphApiAuth:TenantId");
            var clientId = GetRequiredConfig(configuration, "GraphApiAuth:ClientId");
            var managedIdentityClientId = configuration["GraphApiAuth:ManagedIdentityClientId"];

            var logger = sp.GetRequiredService<ILoggerFactory>()
                           .CreateLogger(typeof(ServiceConfigurationExtension).FullName!);

            if (string.IsNullOrWhiteSpace(managedIdentityClientId))
            {
                logger.LogInformation("Graph authentication: client secret for client {ClientId}.", clientId);
                var clientSecret = GetRequiredConfig(configuration, "GraphApiAuth:ClientSecret");
                return new ClientSecretCredential(tenantId, clientId, clientSecret);
            }

            logger.LogInformation(
                "Graph authentication: federated identity credential for client {ClientId} via managed identity {ManagedIdentityClientId}.",
                clientId, managedIdentityClientId);

            return CreateFederatedIdentityCredential(tenantId, clientId, managedIdentityClientId);
        });

        // GraphServiceClient is thread-safe and meant to be reused, so register it as a singleton.
        services.AddSingleton(sp => new GraphServiceClient(sp.GetRequiredService<TokenCredential>(), GraphScopes));

        services.AddScoped<Interfaces.IFtpbEmailProvider, GraphApiEmailProvider>();
        services.Configure<GraphApiEmailSettings>(configuration.GetSection(GraphApiEmailSettings.ConfigSection));
    }

    /// <summary>
    /// Builds a credential that authenticates as the app registration without holding any secret.
    /// </summary>
    private static TokenCredential CreateFederatedIdentityCredential(
        string tenantId,
        string clientId,
        string managedIdentityClientId)
    {
        var managedIdentity = new ManagedIdentityCredential(
            ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId));

        var tokenExchangeContext = new TokenRequestContext([$"{TokenExchangeAudience}/.default"]);

        return new ClientAssertionCredential(tenantId, clientId, async cancellationToken =>
            (await managedIdentity.GetTokenAsync(tokenExchangeContext, cancellationToken)
                                  .ConfigureAwait(false)).Token);
    }

    /// <summary>
    /// Registers the Graph connectivity check under the <c>ready</c> tag.
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
