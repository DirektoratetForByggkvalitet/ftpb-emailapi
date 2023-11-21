using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi
{
    public class GraphApiHealthCheck : IHealthCheck
    {
        private readonly TokenProvider _tokenProvider;

        public GraphApiHealthCheck(TokenProvider tokenProvider)
        {
            _tokenProvider = tokenProvider;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var isHealthy = true;
            string? healthMessage;
            try
            {
                var token = await _tokenProvider.AcquireToken();

                if (token != null)
                    healthMessage = "Able to connect and retreive token from Microsoft Graph";
                else
                {
                    isHealthy = false;
                    healthMessage = "Unable to retreive access token from Microsoft Graph";
                }
            }
            catch (Exception ex)
            {
                isHealthy = false;
                healthMessage = $"Unable to retreive access token from Microsoft Graph; {ex.Message}";
            }

            if (isHealthy)
                return await Task.FromResult(HealthCheckResult.Healthy(healthMessage));

            return await Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, healthMessage));
        }
    }
}