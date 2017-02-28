using Slamby.Common.Helpers;

namespace Slamby.API.Services.Interfaces
{
    public interface ISecretManager
    {
        void Change(string secret);
        bool IsMatch(string text);
        bool IsSet();
        string GetSecret();
        void Load();
        Result Validate(string secret);
    }
}