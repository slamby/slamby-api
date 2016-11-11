namespace Slamby.API.Models
{
    public class SetupModel
    {
        public string Secret { get; set; }

        public int SecretMinLength { get; set; }

        public int SecretMaxLength { get; set; }
    }
}
