using Slamby.API.Models;

namespace Slamby.API.Repositories.Interfaces
{
    public interface IGlobalStoreDataSetRepository : IGlobalStoreRepository<GlobalStoreDataSet>
    {
        void Rename(string name, string newName);

        void AddBusy(string name);
        void RemoveBusy(string name);
        void ThrowIfBusy(string name);
        bool IsBusy(string name);
    }
}
