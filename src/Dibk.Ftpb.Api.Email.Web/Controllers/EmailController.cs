using Dibk.Ftpb.Api.Email.Interfaces;
using Dibk.Ftpb.Api.Email.Models;
using Microsoft.AspNetCore.Mvc;

namespace Dibk.Ftpb.Api.Email;

[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly ILogger<EmailController> _logger;
    private readonly IFtpbEmailProvider _emailProvider;

    public EmailController(ILogger<EmailController> logger, IFtpbEmailProvider emailProvider)
    {
        _logger = logger;
        _emailProvider = emailProvider;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] EmailMessage emailMessage)
    {
        if (emailMessage == null || !ModelState.IsValid)
            return new BadRequestObjectResult(ModelState);

        await _emailProvider.SendEmail(emailMessage);

        return Ok();
    }
}