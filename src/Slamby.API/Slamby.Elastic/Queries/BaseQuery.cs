using System;
using System.Collections.Generic;
using System.Linq;
using Nest;
using Slamby.Elastic.Factories;
using Slamby.Elastic.Models;
using MoreLinq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Slamby.Common.Exceptions;
using Slamby.Common.Config;
using System.Threading;

namespace Slamby.Elastic.Queries
{
    public abstract class BaseQuery
    {
        private static readonly int _maxSizeWithoutScroll = 2000;

        public static string _tokenCountSuffix = "_count";
        public static readonly string _analyzerPrefix = "analyzer_";
        public static string _filterPrefix = "filter_";

        public string IndexName { get; protected set; }
        protected Nest.ElasticClient Client { get; set; }
        protected SiteConfig SiteConfig { get; set; }

        public BaseQuery(ElasticClient client, SiteConfig siteConfig)
        {
            IndexName = client.ConnectionSettings.DefaultIndex;
            Client = client;
            SiteConfig = siteConfig;
        }

        public BaseQuery(ElasticClientFactory elasticClientFactory, string indexName, SiteConfig siteConfig)
        {
            IndexName = indexName;
            Client = elasticClientFactory.GetClient(indexName);
            SiteConfig = siteConfig;
        }

        protected SearchResult<T> Get<T>(SearchDescriptor<T> descriptor) where T : class, IModel, new()
        {
            var size = 0;
            var searchDescriptor = (ISearchRequest)descriptor;

            if (searchDescriptor.Size != null)
            {
                size = searchDescriptor.Size.Value;
            }
            else
            {
                size = Convert.ToInt32(Count(descriptor));
            }
            var results = new List<T>();

            var keepAliveTime = new TimeSpan(0, 0, 100);

            //lapozási méret
            var bulkSize = size > SiteConfig.Resources.MaxSearchBulkCount ? SiteConfig.Resources.MaxSearchBulkCount : size;
            descriptor.Scroll(keepAliveTime);

            var noChance = false;
            ISearchResponse<T> scanResult = null;
            do
            {
                try
                {
                    searchDescriptor.Size = bulkSize;
                    scanResult = Client.Search<T>(descriptor);
                    ResponseValidator(scanResult);
                }
                catch (Exception ex) when (ex is Elasticsearch.Net.UnexpectedElasticsearchClientException || ex is ElasticSearchException)
                {
                    if (ex is Elasticsearch.Net.UnexpectedElasticsearchClientException || scanResult.ServerError?.Status == 503 || scanResult.ServerError?.Status == 429)
                    {
                        if (bulkSize == 1) noChance = true;
                        else
                        {
                            bulkSize = Math.Max((Convert.ToInt32((double)bulkSize / 2)), 1);
                            Thread.Sleep(5000);
                        }
                    }
                    else
                    {
                        throw ex;
                    }
                }
            } while (!scanResult.IsValid || noChance);

            if (noChance) throw new OutOfResourceException("The server doesn't have enough resource to complete the request!");

            results.AddRange(scanResult.Documents);

            //TODO a scroll többi részére sem ártana hasonló, attól hogy egyszeri sikeres lekérés alapján beállítjuk a bulksize-t, még lehet gond

            if (results.Count < size)
            {
                var scrollId = scanResult.ScrollId;

                do
                {
                    var scrollResponse = Client.Scroll<T>(keepAliveTime, scrollId);
                    ResponseValidator(scrollResponse);

                    if (!scrollResponse.Documents.Any())
                    {
                        scrollId = null;
                    }
                    else
                    {
                        scrollId = scrollResponse.ScrollId;
                        results.AddRange(scrollResponse.Documents);

                        //ha már elég elemet adott vissza, akkor abbahagyjuk a scrollozást
                        if (results.Count >= size) scrollId = null;
                    }
                }
                while (scrollId != null);
            }

            //ha több elem jött vissza mint amennyi kellett volna, akkor levágjuk a végét
            if (results.Count > size) results.RemoveRange(size, results.Count - size);

            return new SearchResult<T>
            {
                Items = results,
                Total = scanResult.Total
            };
        }

        protected ScrolledSearchResult<T> GetScrolled<T>(SearchDescriptor<T> descriptor) where T : class, IModel, new()
        {
            const int maxSize = 1000;
            var size = 0;
            var searchDescriptor = (ISearchRequest)descriptor;

            if (searchDescriptor.Size == null)
            {
                size = Convert.ToInt32(Count(descriptor));
            }
            else
            {
                size = searchDescriptor.Size.Value;
            }

            var keepAliveTime = new TimeSpan(0, 0, 100);

            //lapozási méret
            searchDescriptor.Size = size > maxSize ? maxSize : size;

            descriptor.Scroll(keepAliveTime);
            var scanResult = Client.Search<T>(descriptor);

            try
            {
                ResponseValidator(scanResult);
            }
            catch (Exception ex) when (ex is Elasticsearch.Net.UnexpectedElasticsearchClientException || ex is ElasticSearchException)
            {
                if (ex is Elasticsearch.Net.UnexpectedElasticsearchClientException ||
                    scanResult.ServerError?.Status == 503 &&
                    scanResult.ServerError?.Status == 429)
                {
                    throw new OutOfResourceException("The server doesn't have enough resource to complete the request!", ex);
                }

                throw ex;
            }

            return new ScrolledSearchResult<T>
            {
                Items = scanResult.Documents,
                Total = scanResult.Total,
                ScrollId = GetScrollId(scanResult)
            };
        }

        private string GetScrollId<T>(ISearchResponse<T> searchResponse) where T : class
        {
            var count = searchResponse.Hits.Count();
            var scrollId = (count == 0 || count == searchResponse.Total) ? null : searchResponse.ScrollId;
            return scrollId;
        }

        protected ScrolledSearchResult<T> GetScrolled<T>(ScrollDescriptor<T> scrollDescriptor) where T : class, IModel, new()
        {
            var scrollResponse = Client.Scroll<T>(scrollDescriptor);
            
            return new ScrolledSearchResult<T>
            {
                Items = scrollResponse.Documents,
                Total = scrollResponse.Total,
                ScrollId = GetScrollId(scrollResponse)
            };
        }

        protected long Count<T>(SearchDescriptor<T> descriptor) where T : class, IModel
        {
            descriptor.Size(0);
            var result = Client.Search<T>(descriptor);
            ResponseValidator(result);
            return result.Total;
        }

        protected static void ResponseValidator(IResponse response)
        {
            ElasticSearchException exception = null;
            if (!response.IsValid)
            {
                if (response is BulkResponse)
                {
                    var bResp = response as BulkResponse;
                    if (bResp.ItemsWithErrors.Where(i => !i.IsValid && (i.Error?.Type == "mapper_parsing_exception" || i.Error?.Type == "strict_dynamic_mapping_exception")).Any()) return;
                }

                exception = new ElasticSearchException(
                    response.ServerError != null ? 
                        string.Format("ElasticServerError details: ({0}) {1}, {2}", response.ServerError.Status, response.ServerError.Error.Reason, response.ServerError.Error)
                        : "", 
                    response.OriginalException, 
                    response.ServerError);
            }
            if (exception != null)
            {
                throw exception;
            }
        }

        protected NestBulkResponse Index<T>(IEnumerable<T> elasticObjects, bool doFlush, int batchSize = 0) where T : class, IModel, new()
        {
            var allErroredItems = new List<BulkResponseItemBase>();
            var allSucceedItems = new List<BulkResponseItemBase>();
            var elasticObjectList = elasticObjects.ToList();
            if (batchSize == 0) batchSize = SiteConfig.Resources.MaxIndexBulkCount;
            do
            {
                var actualBatch = elasticObjectList.Count > batchSize ? elasticObjectList.Take(batchSize) : elasticObjectList;
                if (!actualBatch.Any())
                {
                    break;
                }
                var bulkResponse = Client.IndexMany(actualBatch);
                ResponseValidator(bulkResponse);

                // a sikereseket kikapjuk az indexelendők listájából
                var succeedIdsDic = bulkResponse.Items.Select(i => i.Id).Distinct().ToDictionary(i => i, i => i);
                allSucceedItems.AddRange(bulkResponse.Items);
                elasticObjectList.RemoveAll(d => succeedIdsDic.ContainsKey(d.Id));

                // ha voltak nem indexálható elemek
                if (bulkResponse.Errors)
                {
                    // ezek a státusz kódok utalnak szerver túlterhelésre
                    var tryAgainItems = bulkResponse.ItemsWithErrors.Where(i => i.Status == 503 || i.Status == 429);

                    // ezek az elemek nem fognak tudni bemenni, nem a túlterhelés a gond, elrakjuk őket, és kiszedjük a listából
                    var erroredItems = bulkResponse.ItemsWithErrors.Except(tryAgainItems);
                    allErroredItems.AddRange(erroredItems);
                    var erroredIdsDic = erroredItems.Select(i => i.Id).Distinct().ToDictionary(id => id, id => id);
                    elasticObjectList.RemoveAll(d => erroredIdsDic.ContainsKey(d.Id));

                    // a batchsize még csökkenthető
                    if (tryAgainItems.Count() > 0 && batchSize > 1)
                    {
                        batchSize = Math.Max(batchSize / 2, 1);
                        Thread.Sleep(5000);
                    }
                    
                    /*
                    // már nincs remény, ezek biza nem akarnak bemenni, elrakjuk őket, és kiszedjük a listából
                    else
                    {
                        allErroredItems.AddRange(tryAgainItems);
                        var tryAgainIdsDic = tryAgainItems.Select(i => i.Id).ToDictionary(id => id, id => id);
                        elasticObjectList.RemoveAll(d => tryAgainIdsDic.ContainsKey(d.Id));
                    }
                    */

                }
            } while (elasticObjectList.Any());


            if (doFlush) ResponseValidator(Client.Flush(IndexName));

            var response = new NestBulkResponse
            {
                Items = allSucceedItems,
                ItemsWithErrors = allErroredItems
            };

            return response;
        }

        public NestBulkResponse ParallelBulkIndex<T>(IEnumerable<T> elasticObjects, int parallelLimit) where T : class, IModel, new()
        {
            return ParallelBulkIndex(elasticObjects, parallelLimit, 0);
        }

        public NestBulkResponse ParallelBulkIndex<T>(IEnumerable<T> elasticObjects, int parallelLimit, decimal objectsSizeInBytes) where T : class, IModel, new()
        {
            // it can be -1 (for Parallel.ForEach means not set), for calculation we need an exact number greater than 0
            if (parallelLimit < 1)
            {
                parallelLimit = Environment.ProcessorCount;
            }

            var allCount = elasticObjects.Count();
            if (allCount == 0) return new NestBulkResponse();

            var batchSize = 0;
            var divider = 0;

            if (objectsSizeInBytes != 0)
            {
                divider = objectsSizeInBytes < SiteConfig.Resources.MaxIndexBulkSize ? 1 : Math.Max(Convert.ToInt32(objectsSizeInBytes / (SiteConfig.Resources.MaxIndexBulkSize)), 1);
            }
            else
            {
                divider = allCount < SiteConfig.Resources.MaxIndexBulkCount ? 1 : Math.Max(Convert.ToInt32((double)allCount / (SiteConfig.Resources.MaxIndexBulkCount)), 1);
            }

            batchSize = (int)Math.Ceiling((double)allCount / divider);
            parallelLimit = Math.Min(divider, parallelLimit);
            var parallelBatchSize = Math.Max(1, (int)Math.Ceiling(allCount / (double)parallelLimit));
            var parallelBatchs = elasticObjects.Batch(parallelBatchSize);

            var bulkResponseStructs = new ConcurrentBag<NestBulkResponse>();

            try
            {
                Parallel.ForEach(parallelBatchs, documents => 
                {
                    var bulkResponse = Index(documents, false, batchSize);
                    bulkResponseStructs.Add(bulkResponse);
                });
            }
            finally
            {
                Client.Flush(IndexName);
            }
            var response = new NestBulkResponse
            {
                Items = new List<BulkResponseItemBase>(),
                ItemsWithErrors = new List<BulkResponseItemBase>()
            };
            foreach (var bResp in bulkResponseStructs)
            {
                response.Items.AddRange(bResp.Items);
                response.ItemsWithErrors.AddRange(bResp.ItemsWithErrors);
            }
            return response;

        }
        
    }
}