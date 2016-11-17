namespace Slamby.API.ViewModels
{
    public class SetupModel
    {
        public string ApplicationId { get; set; }

        public string Secret { get; set; }

        public int SecretMinLength { get; set; }

        public int SecretMaxLength { get; set; }

        public string LicenseKey { get; set; }

        public AlertModel Alert { get; set; }
    }
}
