using System.Collections.Generic;
using System.Linq;
using Nest;
using Newtonsoft.Json.Linq;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Elastic.Factories;
using Slamby.Elastic.Models;

namespace Slamby.Elastic.Queries
{
    [TransientDependency]
    [TransientDependency(ServiceType = typeof(IEnsureIndex))]
    public class FileParserQuery : BaseQuery, IEnsureIndex
    {
        readonly ElasticClientFactory elasticClientFactory;
        readonly IndexQuery indexQuery;

        public FileParserQuery(ElasticClientFactory elasticClientFactory, IndexQuery indexQuery, SiteConfig siteConfig) :
            base(elasticClientFactory, Constants.SlambyFileParserIndex, siteConfig)
        {
            this.indexQuery = indexQuery;
            this.elasticClientFactory = elasticClientFactory;
        }

        public void CreateIndex()
        {
            if (indexQuery.IsExists(Constants.SlambyFileParserIndex))
            {
                return;
            }

            var descriptor = new CreateIndexDescriptor(Constants.SlambyFileParserIndex);
            descriptor
                .Settings(s => s
                    .NumberOfReplicas(0)
                    .NumberOfShards(1))
                .Mappings(m => m
                    .Map<FileParserElastic>(mm => mm
                        .Properties(
                            p => p.Attachment(desc => desc
                                .Name("content")
                                .FileField(d => d //ContentField
                                    .Store(true)
                                    .Analyzer("standard"))
                                .ContentTypeField(d => d.Store(true))
                                .ContentLengthField(d => (d as NumberPropertyDescriptor<FileParserElastic>).Store(true))
                                .LanguageField(d => (d as StringPropertyDescriptor<FileParserElastic>).Store(true))
                                .KeywordsField(d => d.Store(true))
                                .AuthorField(d => d.Store(true))
                                .DateField(d => d.Store(true))
                                .TitleField(d => d.Store(true))
                                )
                            ))
                        );

            var createResp = elasticClientFactory.GetClient().CreateIndex(descriptor);
            ResponseValidator(createResp);
        }

        public Dictionary<string, IEnumerable<object>> ParseDocument(string base64String)
        {
            var fileParser = new FileParserElastic()
            {
                Content = base64String
            };

            var elasticClient = elasticClientFactory.GetClient(Constants.SlambyFileParserIndex);
            var indexResp = elasticClient.Index(fileParser);

            ResponseValidator(indexResp);
            elasticClient.Flush(Constants.SlambyFileParserIndex);

            var sdesc = new SearchDescriptor<FileParserElastic>()
                .Query(q => q
                    .Ids(i => i.Values(indexResp.Id)
                    ))
                .Fields(s => s.Fields($"{nameof(FileParserElastic.Content).ToLowerInvariant()}.*"));

            var sresp = Client.Search<FileParserElastic>(sdesc);
            ResponseValidator(sresp);

            var fieldValues = ((SearchResponse<FileParserElastic>)sresp).Fields.FirstOrDefault();

            Client.Delete<FileParserElastic>(indexResp.Id);

            var returnValue = new Dictionary<string, IEnumerable<object>>();
            var contentField = $"{nameof(FileParserElastic.Content).ToLowerInvariant()}.";

            foreach (var field in fieldValues)
            {
                var key = field.Key.Remove(0, contentField.Length);
                var values = (field.Value as JArray).Select(i => i.ToObject<object>()).ToList();

                returnValue.Add(key, values);
            }

            return returnValue;
        }
    }
}
