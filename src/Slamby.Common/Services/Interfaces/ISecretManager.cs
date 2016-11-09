namespace Slamby.Common.Services.Interfaces
{
    public interface ISecretManager
    {
        void Change(string secret);
        bool IsMatch(string text);
        bool IsSet();
        void Load();
        bool Validate(string secret);
    }
}