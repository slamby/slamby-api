namespace Slamby.SDK.Net.Models
{
    public class License
    {
        public bool IsValid { get; set; }

        public string Message { get; set; }

        public string Type { get; set; }

        public string Base64 { get; set; }
    }
}
