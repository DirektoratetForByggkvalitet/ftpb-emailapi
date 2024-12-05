using System.ComponentModel.DataAnnotations;

namespace Dibk.Ftpb.Api.Email.Models;

public class EmailAttachment
{
    [Required(ErrorMessage = "'FileName' cannot be empty")]
    public string FileName { get; set; }
    [Required(ErrorMessage = "Attachment must have content")]
    public byte[] Content { get; set; }
    [Required(ErrorMessage = "'MimeType' cannot be empty")]
    public string MimeType { get; set; }
}
