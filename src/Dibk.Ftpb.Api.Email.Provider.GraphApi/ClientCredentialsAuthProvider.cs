using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi
{
    public class ClientCredentialsAuthProvider : IAuthenticationProvider
    {
        private readonly string clientId;
        private readonly string clientSecret;
        private readonly string[] appScopes;
        private readonly string tenantId;

        public ClientCredentialsAuthProvider(string clientId, string clientSecret, string[] appScopes, string tenantId)
        {
            this.clientId = clientId;
            this.clientSecret = clientSecret;
            this.appScopes = appScopes;
            this.tenantId = tenantId;
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            var clientApplication = ConfidentialClientApplicationBuilder.Create(this.clientId)
                .WithClientSecret(this.clientSecret)
                .WithClientId(this.clientId)
                .WithTenantId(this.tenantId)
                .Build();

            var result = await clientApplication.AcquireTokenForClient(this.appScopes).ExecuteAsync();
            var h = result.CreateAuthorizationHeader();
            request.Headers.Remove("Authorization");
            request.Headers.Add("Authorization", result.CreateAuthorizationHeader());
        }
    }
}