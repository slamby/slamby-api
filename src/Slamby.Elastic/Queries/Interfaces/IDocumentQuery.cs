using System;
using System.Collections.Generic;
using Slamby.Elastic.Models;

namespace Slamby.Elastic.Queries
{
    public interface IDocumentQuery
    {
        string IndexName { get; }
        long CountAll();
        Dictionary<string, long> CountAll(List<string> indexNames);
        long Count(string tagId = null, string tagField = null);
        Dictionary<string, int> CountForTags(List<string> tagIds, string tagField);
        void Delete(string id);
        void Delete(IEnumerable<string> ids);
        ScrolledSearchResult<DocumentElastic> Filter(string generalQuery, IEnumerable<string> tagIds, string tagField, int limit, string orderBy, bool isDescending, IEnumerable<string> interPretedFields, IEnumerable<string> documentObjectFieldNames, IEnumerable<string> returningDocumentObjectFields, IEnumerable<string> ids = null, DateTime? dateStart = null, DateTime? dateEnd = null, string shouldQuery = null);
        void Flush();
        ScrolledSearchResult<DocumentElastic> GetScrolled(string scrollId);
        DocumentElastic Get(string id);
        IEnumerable<DocumentElastic> Get(IEnumerable<string> ids);
        SearchResult<DocumentElastic> GetAll();
        IEnumerable<DocumentElastic> GetByTagId(string tagId, string tagField, IEnumerable<string> fields = null);
        IEnumerable<DocumentElastic> GetByTagIds(IEnumerable<string> tagIds, string tagField, IEnumerable<string> fields = null);
        Dictionary<string, List<string>> GetExistsForQueries(Dictionary<string, string> queries, List<string> ids);
        SearchResult<DocumentElastic> GetTagIdFieldOnly(string tagField);
        NestBulkResponse Index(IEnumerable<DocumentElastic> documentElastics, bool doFlush = true);
        NestBulkResponse Index(DocumentElastic documentElastic);
        NestBulkResponse ParallelBulkIndex(IEnumerable<DocumentElastic> documentElastics, int parallelLimit, decimal objectsSizeInBytes);
        bool IsExists(string id);
        void Optimize();
        string PrefixQueryFields(string query, IEnumerable<string> documentObjectFieldNames);
        SearchResult<DocumentElastic> Sample(string seed, IEnumerable<string> tagIds, string tagField, int size, IEnumerable<string> fields = null);
        SearchResult<DocumentElastic> Sample(string seed, IEnumerable<string> tagIds, string tagField, double percent, IEnumerable<string> fields = null);
        string Update(string id, DocumentElastic documentElastic);
    }
}