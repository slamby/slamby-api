using System.Collections.Generic;

namespace Slamby.API.Repositories
{
    public interface IGlobalStoreServiceAliasRepository
    {
        IDictionary<string, string> ServiceAliases { get; }

        string Get(string alias);
        bool IsExist(string alias);
        void Remove(string alias);
        void Set(string alias, string id);
    }
}