using System.ComponentModel.DataAnnotations;

namespace Dibk.Ftpb.Api.Email.Models
{
    public class EmailAddress
    {
        [Required]
        [EmailAddress]
        public string Address { get; set; }
        public string DisplayName { get; set; }
    }
}
