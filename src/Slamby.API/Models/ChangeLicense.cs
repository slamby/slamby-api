using System.ComponentModel.DataAnnotations;

namespace Slamby.SDK.Net.Models
{
    public class ChangeLicense
    {
        [Required]
        public string License { get; set; }
    }
}
