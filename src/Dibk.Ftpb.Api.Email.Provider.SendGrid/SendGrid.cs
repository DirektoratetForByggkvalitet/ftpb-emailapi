using Dibk.Ftpb.Api.Email.Interfaces;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Linq;
using System.Threading.Tasks;
using EmailAddress = SendGrid.Helpers.Mail.EmailAddress;

namespace Dibk.Ftpb.Api.Email.Provider
{
    public class SendGridProvider : IFtpbEmailProvider
    {
        private readonly ILogger<SendGridProvider> _logger;
        private readonly ISendGridClient _sendGridClient;

        public SendGridProvider(ILogger<SendGridProvider> logger, ISendGridClient sendGridClient)
        {
            _logger = logger;
            _sendGridClient = sendGridClient;
        }
        public async Task SendEmail(Models.EmailMessage email)
        {
            var sendGridMessage = new SendGridMessage();
            sendGridMessage.AddTos(email.To.Select(t => new EmailAddress(t.Address, t.DisplayName)).ToList());
            if (email.CC != null && email.CC.Any())
                sendGridMessage.AddCcs(email.CC.Select(t => new EmailAddress(t.Address, t.DisplayName)).ToList());

            if (email.Bcc != null && email.Bcc.Any())
                sendGridMessage.AddBccs(email.Bcc.Select(t => new EmailAddress(t.Address, t.DisplayName)).ToList());

            sendGridMessage.SetSubject(email.Subject);

            if (!string.IsNullOrWhiteSpace(email.HtmlBody))
                sendGridMessage.AddContent(MimeType.Html, email.HtmlBody);

            sendGridMessage.AddContent(MimeType.Text, email.Body);
            sendGridMessage.From = new EmailAddress(email.From.Address, email.From.DisplayName);

            await _sendGridClient.SendEmailAsync(sendGridMessage);
        }
    }


}
