using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi;

public class ClientCredentialsAuthProvider : IAuthenticationProvider
{
    private readonly TokenProvider _tokenProvider;

    public ClientCredentialsAuthProvider(TokenProvider tokenProvider)
    {
        this._tokenProvider = tokenProvider;
    }

    public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        var result = await _tokenProvider.AcquireToken();
        var h = result.CreateAuthorizationHeader();
        request.Headers.Remove("Authorization");
        request.Headers.Add("Authorization", result.CreateAuthorizationHeader());
    }
}