using Azure.Core;
using Azure.Identity;
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

    /// <summary>Audience Entra requires on a managed-identity token used as a client assertion.</summary>
    private const string TokenExchangeAudience = "api://AzureADTokenExchange";

    public static void AddGraphApiEmailProvider(this IServiceCollection services, IConfiguration configuration)
    {
        // Authenticate to Microsoft Graph as the app registration

        // Registered as a singleton because both credentials cache tokens internally.
        services.AddSingleton<TokenCredential>(_ =>
        {
            var tenantId = GetRequiredConfig(configuration, "GraphApiAuth:TenantId");
            var clientId = GetRequiredConfig(configuration, "GraphApiAuth:ClientId");
            var managedIdentityClientId = GetRequiredConfig(configuration, "GraphApiAuth:ManagedIdentityClientId");

            var managedIdentity = new ManagedIdentityCredential(
                ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId));

            var tokenExchangeContext = new TokenRequestContext([$"{TokenExchangeAudience}/.default"]);

            return new ClientAssertionCredential(tenantId, clientId, async cancellationToken =>
                (await managedIdentity.GetTokenAsync(tokenExchangeContext, cancellationToken)
                                      .ConfigureAwait(false)).Token);
        });

        // GraphServiceClient is thread-safe and meant to be reused, so register it as a singleton.
        services.AddSingleton(sp => new GraphServiceClient(sp.GetRequiredService<TokenCredential>(), GraphScopes));

        services.AddScoped<Interfaces.IFtpbEmailProvider, GraphApiEmailProvider>();
        services.Configure<GraphApiEmailSettings>(configuration.GetSection(GraphApiEmailSettings.ConfigSection));
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
