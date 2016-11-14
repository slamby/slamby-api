namespace Slamby.API.Services
{
    public interface ILicenseManager
    {
        string ApplicationId { get; }
        string InstanceId { get; }

        void EnsureCreated();
    }
}