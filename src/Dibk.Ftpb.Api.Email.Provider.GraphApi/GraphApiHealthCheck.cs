using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi;

public class GraphApiHealthCheck : IHealthCheck
{
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];
    private readonly IServiceProvider _serviceProvider;

    public GraphApiHealthCheck(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve the credential here (not via constructor injection) so that a
            // configuration or Key Vault failure surfaces as Unhealthy instead of a 500.
            var tokenCredential = _serviceProvider.GetRequiredService<TokenCredential>();
            var token = await tokenCredential.GetTokenAsync(new TokenRequestContext(GraphScopes), cancellationToken);

            if (!string.IsNullOrEmpty(token.Token))
                return HealthCheckResult.Healthy("Able to connect and retrieve token from Microsoft Graph");

            return new HealthCheckResult(context.Registration.FailureStatus, "Unable to retrieve access token from Microsoft Graph");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"Unable to retrieve access token from Microsoft Graph; {ex.Message}");
        }
    }
}
