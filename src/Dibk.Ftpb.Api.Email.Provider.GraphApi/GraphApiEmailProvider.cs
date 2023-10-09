using Dibk.Ftpb.Api.Email.Interfaces;
using Dibk.Ftpb.Api.Email.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace Dibk.Ftpb.Api.Email.Provider.GraphApi
{
    public partial class GraphApiEmailProvider : IFtpbEmailProvider
    {
        private readonly ILogger<GraphApiEmailProvider> _logger;
        private readonly IAuthenticationProvider _clientCredentialsAuthProvider;
        private readonly GraphApiEmailSettings _settings;

        public GraphApiEmailProvider(ILogger<GraphApiEmailProvider> logger, IAuthenticationProvider clientCredentialsAuthProvider, IOptions<GraphApiEmailSettings> options)
        {
            _logger = logger;
            _clientCredentialsAuthProvider = clientCredentialsAuthProvider;
            _settings = options.Value;
        }
        public async Task SendEmail(EmailMessage email)
        {
            Message message = BuildMessage(email);

            GraphServiceClient client = new GraphServiceClient(_clientCredentialsAuthProvider);
            var saveToSentItems = false;
            try
            {
                await client.Users[_settings.UserPrincipalName]
                    .SendMail(message, saveToSentItems)
                    .Request()
                    .PostAsync();
                _logger.LogInformation("Email sendt");
            }
            catch (Microsoft.Graph.ServiceException svcEx)
            {
                var s = svcEx.ToString();
                var errorMessage =  svcEx.Error.ToString();

                _logger.LogError(svcEx, $"{s} - {errorMessage}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred when sending message");
                throw;
            }
        }

        private Message BuildMessage(EmailMessage email)
        {
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

            if (email.Attachments?.Count() > 0)
            {
                var attachments = new MessageAttachmentsCollectionPage();

                foreach (var attachment in email.Attachments)
                {
                    attachments.Add(new FileAttachment() { ContentBytes = attachment.Content, Name = attachment.FileName });
                }

                message.Attachments = attachments;
            }

            if (email.From == null || string.IsNullOrEmpty(email.From.Address))
                message.From = new Recipient() { EmailAddress = new Microsoft.Graph.EmailAddress() { Name = _settings.DefaultFromDisplayName, Address = _settings.DefaultFromAddress } };
            else
                message.From = new Recipient() { EmailAddress = new Microsoft.Graph.EmailAddress() { Name = email.From.DisplayName, Address = email.From.Address } };

            message.ToRecipients = email.To.Select(p => new Recipient() { EmailAddress = new Microsoft.Graph.EmailAddress() { Address = p.Address, Name = p.DisplayName } }).ToList();

            _logger.LogDebug("Email message built");

            return message;
        }
    }
}