using Slamby.API.Repositories;
using Slamby.API.Repositories.Interfaces;
using Slamby.API.Services.Interfaces;
using Slamby.Common.DI;

namespace Slamby.API.Services
{
    [SingletonDependency(ServiceType = typeof(IGlobalStoreManager))]
    public class GlobalStoreManager : IGlobalStoreManager
    {
        public GlobalStoreManager(IGlobalStoreDataSetRepository dataSetRepository,
            IGlobalStoreProcessRepository processesRepository,
            IGlobalStoreClassifierRepository classifiersRepository,
            IGlobalStorePrcRepository prcsRepository,
            IGlobalStoreServiceAliasRepository serviceAliasesRepository,
            IGlobalStoreSearchRepository searchRepository)
        {
            DataSets = dataSetRepository;
            Processes = processesRepository;
            ActivatedClassifiers = classifiersRepository;
            ActivatedPrcs = prcsRepository;
            ServiceAliases = serviceAliasesRepository;
            ActivatedSearches = searchRepository;
        }

        public IGlobalStoreDataSetRepository DataSets { get; } = new GlobalStoreDataSetRepository();

        public IGlobalStoreProcessRepository Processes { get; } = new GlobalStoreProcessRepository();

        public IGlobalStoreClassifierRepository ActivatedClassifiers { get; } = new GlobalStoreClassifierRepository();

        public IGlobalStorePrcRepository ActivatedPrcs { get; } = new GlobalStorePrcRepository();

        public IGlobalStoreServiceAliasRepository ServiceAliases { get; } = new GlobalStoreServiceAliasRepository();

        public IGlobalStoreSearchRepository ActivatedSearches { get; } = new GlobalStoreSearchRepository();
    }
}