using Dibk.Ftpb.Api.Email.Interfaces;
using Dibk.Ftpb.Api.Email.Models;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dibk.Ftpb.Api.Email.Provider.Office365
{
    public class Office365EmailProvider : IFtpbEmailProvider
    {
        private readonly IOptions<Office365EmailSettings> _options;
        private readonly ILogger<Office365EmailProvider> _logger;

        public Office365EmailProvider(IOptions<Office365EmailSettings> options, ILogger<Office365EmailProvider> logger)
        {
            _options = options;
            _logger = logger;
        }

        public async Task SendEmail(EmailMessage email)
        {
            var message = new MimeMessage();
            if (email.From == null || string.IsNullOrEmpty(email.From.Address))
                message.From.Add(new MailboxAddress(_options.Value.DefaultFromDisplayName, _options.Value.DefaultFromAddress));
            else
                message.From.Add(new MailboxAddress(email.From.DisplayName, email.From.Address));

            message.To.AddRange(email.To.Select(s => new MailboxAddress(s.DisplayName, s.Address)).ToList());

            if (email.CC != null && email.CC.Count() > 0)
                message.Cc.AddRange(email.CC.Select(s => new MailboxAddress(s.DisplayName, s.Address)).ToList());

            if (email.Bcc != null && email.Bcc.Count() > 0)
                message.Bcc.AddRange(email.Bcc.Select(s => new MailboxAddress(s.DisplayName, s.Address)).ToList());

            message.Subject = email.Subject;

            var bb = new BodyBuilder();
            bb.HtmlBody = email.HtmlBody;
            bb.TextBody = email.Body;

            if (email.Attachments?.Count() > 0)
            {
                foreach (var attachment in email.Attachments)
                {
                    bb.Attachments.Add(attachment.FileName, attachment.Content);
                }
            }

            message.Body = bb.ToMessageBody();
            _logger.LogDebug("Email message built");

            using (var client = new SmtpClient())
            {
                await ConnectToMailServer(client);
                await AuthenticateWithMailserver(client);

                try
                {
                    await client.SendAsync(message);
                    _logger.LogInformation("Email sendt");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception occurred when sending message");
                    throw;
                }
                await client.DisconnectAsync(true);
            }
        }

        private async Task AuthenticateWithMailserver(SmtpClient client)
        {
            try
            {
                await client.AuthenticateAsync(_options.Value.Username, _options.Value.Password);
                _logger.LogDebug("Authenticated with mailserver");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unable to authenticate with mailserver");
                throw;
            }
        }

        private async Task ConnectToMailServer(SmtpClient client)
        {
            try
            {
                await client.ConnectAsync("smtp.office365.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                _logger.LogDebug("Connected to mailserver");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unable to connect to mail server");
                throw;
            }
        }
    }
}
