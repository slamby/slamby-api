using System;
using System.Collections.Generic;
using System.Linq;
using Nest;
using Newtonsoft.Json.Linq;
using Slamby.Common.DI;
using Slamby.Elastic.Factories;
using Slamby.Elastic.Helpers;
using Slamby.Elastic.Models;
using Slamby.Common.Config;
using System.Reflection;

namespace Slamby.Elastic.Queries
{
    [TransientDependency]
    public class IndexQuery : BaseQuery
    {
        readonly ElasticClientFactory clientFactory;
        readonly ElasticClient defaultClient;

        private const string DefaultTokenizer = "standard";
        private const string DefaultCharFilter = "html_filter";


        public IndexQuery(ElasticClient client, ElasticClientFactory clientFactory, SiteConfig siteConfig) : base(client, siteConfig)
        {
            this.clientFactory = clientFactory;
            this.defaultClient = clientFactory.GetClient();
        }

        public void CreateIndex(string name, string indexName, int shingleCount, object sampleDynamicDocument, string idField, List<string> interpretedFields, string tagField)
        {
            try
            {
                #region Default Index creation

                var tokenFilters = CreateTokenFilterDescriptor();
                var analyzers = CreateAnalyzersDescriptor(shingleCount, DefaultTokenizer, DefaultCharFilter, tokenFilters);
                var analysisDescriptor = CreateAnalysisDescriptor(DefaultCharFilter, tokenFilters, analyzers);
                var indexDescriptor = CreateIndexDescriptor(indexName, analysisDescriptor,
                    (map) => map
                        .Dynamic(true)
                        .DateDetection(false)
                        .NumericDetection(false)
                        .AutoMap());
                var createResp = defaultClient.CreateIndex(indexDescriptor);
                ResponseValidator(createResp);

                #endregion

                var client = clientFactory.GetClient(indexName);

                #region Dynamic Document indexing

                var dynamicDocumentType = DocumentElastic.DocumentTypeName;
                var sampleId = "sample_id";

                var req = new IndexRequest<DocumentElastic>(new DocumentElastic { DocumentObject = sampleDynamicDocument }, indexName, dynamicDocumentType, sampleId);
                var indexResp = client.Index<object>(req);
                ResponseValidator(indexResp);
                var deleteResp = client.Delete(new DeleteRequest(indexName, dynamicDocumentType, sampleId));
                ResponseValidator(deleteResp);
                #endregion

                client.Flush(indexName);

                #region Malformed string for Date and Number types

                //if a number or date property is a string in the json, then it makes an exception during the index if we don't put IgnoreMalformed on the property

                var mappingResponse = client.GetMapping(new GetMappingRequest(indexName, dynamicDocumentType));
                ResponseValidator(mappingResponse);
                var prop = mappingResponse.Mapping.Properties[DocumentElastic.DocumentObjectMappingName];
                var canMalformedPropsKvp = _setIgnoreMalformedDateAndNumberPropertyNames(prop);
                var canMalformedProps = new Properties(canMalformedPropsKvp.GroupBy(x => x.Key).Select(g => g.First()).ToDictionary(c => c.Key, c => c.Value));

                var canMalformedPropDesc = new PropertiesDescriptor<object>(canMalformedProps);

                var putMappingDescMalformed = new PutMappingDescriptor<DocumentElastic>()
                    .Index(indexName)
                    .Properties(p => p.Object<object>(o => o.Name(DocumentElastic.DocumentObjectMappingName).Properties(op => canMalformedPropDesc)));
                var mapMalformedResp = client.Map<DocumentElastic>(p => putMappingDescMalformed);
                ResponseValidator(mapMalformedResp);

                #endregion

                #region Interpreted fields and text field mapping

                var ipfPropDesc = CreateInterpretedFieldsPropertyDescriptor();
                var interpretedFieldsPropDesc = new PropertiesDescriptor<object>();
                var segments = interpretedFields.OrderBy(field => field)
                                                .Select(field => field
                                                    .Split(new[] { "." }, StringSplitOptions.None))
                                                .ToArray();
                IndexHelper.SetInterpretedFields(interpretedFieldsPropDesc, ipfPropDesc, segments);


                var multiPropDesc = CreateAnalyzerPropertiesDescriptor(shingleCount);
                var putMappingDesc = new PutMappingDescriptor<DocumentElastic>()
                    .Index(indexName)
                    .Dynamic(DynamicMapping.Strict)
                    .Properties(pr => pr
                        .String(map => map
                            .Name(DocumentElastic.TextField)
                            .Fields(f => multiPropDesc))
                        .Object<object>(map => map
                            .Name(DocumentElastic.DocumentObjectMappingName)
                            .Properties(op => interpretedFieldsPropDesc)));
                var mapResp = client.Map<DocumentElastic>(p => putMappingDesc);
                ResponseValidator(mapResp);

                #endregion

                client.Flush(indexName);

                #region Add index property

                AddIndexProperty(client, name, shingleCount, sampleDynamicDocument, null, idField, interpretedFields, tagField);

                #endregion

                client.Flush(indexName);

                CreateAlias(indexName, name);
            }
            catch (Exception ex)
            {
                defaultClient.DeleteIndex(indexName);
                throw ex;
            }
        }

        public void CreateIndexWithSchema(string name, string indexName, int shingleCount, object schema, string idField, List<string> interpretedFields, string tagField)
        {
            var token = JObject.FromObject(schema);

            try
            {
                #region Default Index creation

                var tokenFilters = CreateTokenFilterDescriptor();
                var analyzers = CreateAnalyzersDescriptor(shingleCount, DefaultTokenizer, DefaultCharFilter, tokenFilters);
                var analysisDescriptor = CreateAnalysisDescriptor(DefaultCharFilter, tokenFilters, analyzers);
                var multiPropDesc = CreateAnalyzerPropertiesDescriptor(shingleCount);
                var ipfPropDesc = CreateInterpretedFieldsPropertyDescriptor();

                var indexDescriptor = CreateIndexDescriptor(indexName, analysisDescriptor,
                    (map) => map
                        .Properties(p => p
                            .String(s => s
                                .Name(DocumentElastic.TextField)
                                .Fields(f => multiPropDesc))
                            .Object<object>(o => o
                                .Name(DocumentElastic.DocumentObjectMappingName)
                                .Properties(op => IndexHelper.MapProperties(token, op, ipfPropDesc, interpretedFields)))
                                ));
                var createResp = defaultClient.CreateIndex(indexDescriptor);
                ResponseValidator(createResp);

                #endregion

                var client = clientFactory.GetClient(indexName);

                #region Add index property

                AddIndexProperty(client, name, shingleCount, null, schema, idField, interpretedFields, tagField);

                #endregion

                client.Flush(indexName);

                CreateAlias(indexName, name);
            }
            catch (Exception ex)
            {
                DeleteIfExist(indexName);
                throw ex;
            }
        }

        private static CreateIndexDescriptor CreateIndexDescriptor(string indexName, AnalysisDescriptor analysistDescriptor, Func<TypeMappingDescriptor<DocumentElastic>, TypeMappingDescriptor<DocumentElastic>> documentMappingDescriptor)
        {
            var descriptor = new CreateIndexDescriptor(indexName);
            descriptor.Settings(s => s.NumberOfReplicas(0).NumberOfShards(1).Analysis(a => analysistDescriptor))
                .Mappings(mapping => mapping
                    .Map<DocumentElastic>(map => documentMappingDescriptor(map).AutoMap())
                    .Map<TagElastic>(mm => mm.AutoMap().Dynamic(false))
                    .Map<PropertiesElastic>(mm => mm.AutoMap().Dynamic(false)));
            return descriptor;
        }

        private static AnalysisDescriptor CreateAnalysisDescriptor(string charHtmlFilter, TokenFiltersDescriptor tokenFilters, AnalyzersDescriptor analyzers)
        {
            var analysisDescriptor = new AnalysisDescriptor();
            analysisDescriptor.CharFilters(c => c.HtmlStrip(charHtmlFilter));
            analysisDescriptor.TokenFilters(t => tokenFilters);
            analysisDescriptor.Analyzers(a => analyzers);
            return analysisDescriptor;
        }

        private void AddIndexProperty(ElasticClient client, string name, int shingleCount, object sampleDynamicDocument, object schema, string idField, List<string> interpretedFields, string tagField)
        {
            var indexPropResp = client.IndexMany(new List<PropertiesElastic> {
                new PropertiesElastic
                {
                    IdField = idField,
                    InterPretedFields = interpretedFields,
                    NGramCount = shingleCount,
                    SampleDocument = sampleDynamicDocument,
                    Schema = schema,
                    TagField = tagField,
#pragma warning disable CS0618 // Type or member is obsolete
                    DBVersion = Common.Constants.DBVersion,
#pragma warning restore CS0618 // Type or member is obsolete
                    Name = name
                }
            });

            ResponseValidator(indexPropResp);
            if (indexPropResp.Items.Count() != 1)
            {
                //TODO throw a slamby exception
                throw new Exception("Index creation failed (Property indexing)!");
            }
        }

        private AnalyzersDescriptor CreateAnalyzersDescriptor(int shingleCount, string tokenizer, string charHtmlFilter, TokenFiltersDescriptor tokenFilters)
        {
            var analyzers = new AnalyzersDescriptor();
            for (var i = 1; i <= shingleCount; i++)
            {
                var actualIndex = i;

                var filterName = string.Format("{0}{1}", _filterPrefix, actualIndex);
                if (i != 1)
                {
                    var filterDescriptor =
                        new ShingleTokenFilterDescriptor().MinShingleSize(actualIndex)
                            .MaxShingleSize(actualIndex)
                            .OutputUnigrams(false)
                            .OutputUnigramsIfNoShingles(false);
                    tokenFilters.Shingle(filterName, desc => filterDescriptor);
                }

                var analyzerName = string.Format("{0}{1}", _analyzerPrefix, actualIndex);
                var analyzer =
                    i != 1
                        ? new CustomAnalyzer
                        {
                            Filter = new List<string> { "lowercase", "word_filter", filterName, "filler_filter" },
                            Tokenizer = tokenizer,
                            CharFilter = new List<string> { charHtmlFilter }
                        }
                        : new CustomAnalyzer { Filter = new List<string> { "lowercase", "word_filter", "filler_filter" }, Tokenizer = tokenizer, CharFilter = new List<string> { charHtmlFilter } };
                analyzers.Custom(analyzerName, a => analyzer);
            }

            return analyzers;
        }

        private TokenFiltersDescriptor CreateTokenFilterDescriptor()
        {
            var tokenFilters = new TokenFiltersDescriptor();

            tokenFilters.WordDelimiter("word_filter", w => w
                .GenerateWordParts(true)
                .GenerateNumberParts(true)
                .CatenateWords(false)
                .CatenateNumbers(false)
                .CatenateAll(false)
                .SplitOnCaseChange(false)
                .PreserveOriginal(false)
                .SplitOnNumerics(false)
                .StemEnglishPossessive(false)
            );

            tokenFilters.PatternReplace("filler_filter", p => p
                .Pattern(".*" + Common.Constants.TextFieldSeparator + ".*")
                .Replacement(string.Empty)
            );

            return tokenFilters;
        }


        private PropertiesDescriptor<object> CreateInterpretedFieldsPropertyDescriptor()
        {
            //have to put limit on this field because if term/string is longer than the 32766 bytes it can't be stored in Lucene
            var propDesc = new PropertiesDescriptor<object>();
            propDesc.String(desc => desc
                .Name("raw")
                .Index(FieldIndexOption.NotAnalyzed).IgnoreAbove(10000).IncludeInAll(false));
            var analyzerName = string.Format("{0}{1}", _analyzerPrefix, 1);
            propDesc.String(desc => desc
                    .Name("1")
                    .Analyzer(analyzerName)
                    .SearchAnalyzer(analyzerName)
                    .TermVector(TermVectorOption.WithPositionsOffsetsPayloads));
            
            // we only store the count for unigrams, because it's easy to calculate the others from this
            propDesc.TokenCount(desc => desc
                .Name("1" + _tokenCountSuffix)
                .Analyzer(analyzerName)
                .DocValues(true)
                .Store(true));

            return propDesc;
        }

        private PropertiesDescriptor<object> CreateAnalyzerPropertiesDescriptor(int shingleCount)
        {
            var multiPropDesc = new PropertiesDescriptor<object>();

            for (var i = 1; i <= shingleCount; i++)
            {
                var actualIndex = i;
                var analyzerName = string.Format("{0}{1}", _analyzerPrefix, actualIndex);
                multiPropDesc.String(desc => desc
                    .Name(actualIndex.ToString())
                    .Analyzer(analyzerName)
                    .SearchAnalyzer(analyzerName)
                    .TermVector(TermVectorOption.WithPositionsOffsetsPayloads));
            }

            return multiPropDesc;
        }

        private static List<KeyValuePair<PropertyName, IProperty>> _setIgnoreMalformedDateAndNumberPropertyNames(IProperty prop)
        {
            var respList = new List<KeyValuePair<PropertyName, IProperty>>();
            if (prop is DateProperty || prop is NumberProperty)
            {
                if (prop is DateProperty) ((DateProperty)prop).IgnoreMalformed = true;
                if (prop is NumberProperty) ((NumberProperty)prop).IgnoreMalformed = true;

                respList.Add(new KeyValuePair<PropertyName, IProperty>(prop.Name.Name, prop));
                return respList;
            }
            if (prop is ObjectProperty)
            {
                foreach (var nestedProp in ((ObjectProperty)prop).Properties.Select(p => p.Value))
                {
                    respList.AddRange(_setIgnoreMalformedDateAndNumberPropertyNames(nestedProp));
                }
            }
            return respList;
        }

        public Dictionary<string, PropertiesElastic> GetProperties(string indexName)
        {
            var hits = GetPropertiesHit(indexName);
            return hits.ToDictionary(h => h.Key, h => h.Value.Source);
        }

        /// <summary>
        /// </summary>
        /// <param name="indexName">if it's empty or null, than all the properties get back (for all indices)</param>
        /// <returns></returns>
        public Dictionary<string, IHit<PropertiesElastic>> GetPropertiesHit(string indexName)
        {
            var sdesc = new SearchDescriptor<PropertiesElastic>();
            ISearchResponse<PropertiesElastic> searchResponse;
            if (string.IsNullOrEmpty(indexName)) {
                sdesc.Index(Indices.AllIndices);
                var client = clientFactory.GetClient();
                var size = client.Search<PropertiesElastic>(sdesc).Total;
                sdesc.Size((int)size);
                searchResponse = client.Search<PropertiesElastic>(sdesc);
            }
            else
            {
                searchResponse = Client.Search<PropertiesElastic>(sdesc);
            }
            
            ResponseValidator(searchResponse);
            return searchResponse?.Hits.ToDictionary(h => h.Index, h => h);
        }

        public void Delete(string indexName)
        {
            var deleteResp = Client.DeleteIndex(Indices.Index(indexName));
            ResponseValidator(deleteResp);
        }

        private void DeleteIfExist(string indexName)
        {
            if (IsExists(indexName))
            {
                Delete(indexName);
            }
        }

        public bool IsExists(string indexName)
        {
            return defaultClient.IndexExists(Indices.Index(indexName)).Exists;
        }

        public CatIndicesRecord GetCats(string indexName, bool includeReserved = false)
        {
            return _getCats(indexName, includeReserved)[indexName];
        }

        public Dictionary<string, CatIndicesRecord> GetCats(bool includeReserved = false)
        {
            return _getCats(Indices.AllIndices, includeReserved);
        }

        private Dictionary<string, CatIndicesRecord> _getCats(Indices indices, bool includeReserved)
        {
            var client = clientFactory.GetClient();
            var resp = client.CatIndices(s => s.Index(indices));
            ResponseValidator(resp);

            var result = resp.Records.ToDictionary(r => r.Index, r => r);

            if (!includeReserved)
            {
                foreach (var reservedIndex in Elastic.Constants.ReservedIndices)
                {
                    if (result.ContainsKey(reservedIndex))
                    {
                        result.Remove(reservedIndex);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Checks wether Index or Alias name already exists
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsNameUnique(string name)
        {
            return !IsExists(name) && !IsAliasExist(name);
        }

        /// <summary>
        /// Gets Indexes with their assigned Alias list
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, IList<AliasDefinition>> GetAliases(bool includeReserved = false)
        {
            var resp = defaultClient.GetAlias();

            ResponseValidator(resp);

            if (!includeReserved)
            {
                foreach (var reservedIndex in Constants.ReservedIndices)
                {
                    if (resp.Indices.ContainsKey(reservedIndex))
                    {
                        resp.Indices.Remove(reservedIndex);
                    }
                }
            }
            return resp.Indices;
        }

        /// <summary>
        /// Get assigned Aliases to an Index
        /// </summary>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public IList<AliasDefinition> GetAliasForIndex(string indexName)
        {
            return defaultClient.GetAliasesPointingToIndex(indexName);
        }

        /// <summary>
        /// Checks if alias already exists
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        public bool IsAliasExist(string alias)
        {
            return defaultClient.AliasExists(aed => aed.Name(alias)).Exists;
        }

        public void CreateAlias(string index, string alias)
        {
            var createResp = defaultClient.Alias(desc => desc
                .Add(aad => aad
                    .Index(index)
                    .Alias(alias)));
            ResponseValidator(createResp);
        }

        public void RemoveAlias(string index, string alias)
        {
            var removeResp = defaultClient.Alias(desc => desc
                .Remove(rs => rs
                    .Index(index)
                    .Alias(alias)));
            ResponseValidator(removeResp);
        }
    }
}
