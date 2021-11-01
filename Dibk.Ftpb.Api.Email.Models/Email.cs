using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Dibk.Ftpb.Api.Email.Models
{
    public class EmailMessage : IValidatableObject
    {
        [Required]
        public IEnumerable<EmailAddress> To { get; set; }
        public IEnumerable<EmailAddress> CC { get; set; }
        public IEnumerable<EmailAddress> Bcc { get; set; }
        public EmailAddress From { get; set; }
        [Required]
        public string Subject { get; set; }        
        public string Body { get; set; }
        public string HtmlBody { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (To == null || To.Count() == 0)
                yield return new ValidationResult("To must contain addresses");

            if (string.IsNullOrEmpty(Body) && string.IsNullOrEmpty(HtmlBody))
                yield return new ValidationResult("Email must contain either Body or HtmlBody");
        }
    }
}
