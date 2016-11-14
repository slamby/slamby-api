namespace Slamby.Common.Services.Interfaces
{
    public interface IStore
    {
        bool Exists();

        bool HasContent();

        string Read();

        void Write(string content);
    }
}