using Dibk.Ftpb.Api.Email.Models;
using System.Threading.Tasks;

namespace Dibk.Ftpb.Api.Email.Interfaces;

public interface IFtpbEmailProvider
{
    Task SendEmail(EmailMessage email);
}
