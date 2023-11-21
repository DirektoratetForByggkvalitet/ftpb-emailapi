using Microsoft.Graph;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi
{
    public class ClientCredentialsAuthProvider : IAuthenticationProvider
    {
        private readonly TokenProvider _tokenProvider;

        public ClientCredentialsAuthProvider(TokenProvider tokenProvider)
        {
            this._tokenProvider = tokenProvider;
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            var result = await _tokenProvider.AcquireToken();
            var h = result.CreateAuthorizationHeader();
            request.Headers.Remove("Authorization");
            request.Headers.Add("Authorization", result.CreateAuthorizationHeader());
        }
    }
}