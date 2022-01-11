using Dibk.Ftpb.Api.Email.Interfaces;
using Dibk.Ftpb.Api.Email.Models;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi
{
    public class GraphApiEmailProvider : IFtpbEmailProvider
    {
        private readonly IAuthenticationProvider _clientCredentialsAuthProvider;

        public GraphApiEmailProvider(IAuthenticationProvider clientCredentialsAuthProvider)
        {
            _clientCredentialsAuthProvider = clientCredentialsAuthProvider;
        }
        public async Task SendEmail(EmailMessage email)
        {            
            GraphServiceClient client = new GraphServiceClient(_clientCredentialsAuthProvider);


            var message = new Message();

            message.Subject = email.Subject;

            ItemBody? body = null;

            if (string.IsNullOrEmpty(email.HtmlBody))
                body = new ItemBody()
                {
                    ContentType = BodyType.Text,
                    Content = email.Body
                };
            else
                body = new ItemBody()
                {
                    ContentType = BodyType.Html,
                    Content = email.HtmlBody
                };

            message.Body = body;

            message.ToRecipients = email.To.Select(p =>  new Recipient() { EmailAddress = new Microsoft.Graph.EmailAddress() { Address = p.Address, Name = p.DisplayName} }).ToList();
            message.From = new Recipient() { EmailAddress = new Microsoft.Graph.EmailAddress() { Name = "DIBK", Address = "ikkesvar@dibk.no" } };
            var saveToSentItems = false;

            await client.Me
                .SendMail(message, saveToSentItems)
                .Request()
                .PostAsync();
        }
    }

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