using Slamby.API.Repositories;
using Slamby.API.Repositories.Interfaces;

namespace Slamby.API.Services.Interfaces
{
    public interface IGlobalStoreManager
    {
        IGlobalStoreClassifierRepository ActivatedClassifiers { get; }
        IGlobalStorePrcRepository ActivatedPrcs { get; }
        IGlobalStoreDataSetRepository DataSets { get; }
        IGlobalStoreProcessRepository Processes { get; }
        IGlobalStoreServiceAliasRepository ServiceAliases { get; }
        IGlobalStoreSearchRepository ActivatedSearches { get; }
        string InstanceId { get; set; }
    }
}