using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Slamby.Common.Helpers;
using Slamby.Elastic.Models;
using Slamby.SDK.Net.Models;
using System.Threading;

namespace Slamby.API.Services.Interfaces
{
    public interface IDocumentService
    {
        void CleanDocuments(string dataSetName, List<string> tagIds);
        void Delete(string dataSetName, string id);
        PaginatedList<object> Filter(string dataSetName, string generalQuery, List<string> tagIds, int limit, string orderBy, bool isDescending, List<string> documentObjectFields = null);
        DocumentElastic Get(string dataSetName, string id);
        HashSet<string> GetTagIds(IEnumerable<DocumentElastic> documents, string tagField);
        HashSet<string> GetTagIds(DocumentElastic document, string fieldName);
        Result Index(string dataSetName, object dynamicDocument, string id);
        NestBulkResponse Index(string dataSetName, DocumentElastic documentElastic);
        bool IsExists(string dataSetName, string id);
        Result Update(string dataSetName, string id, DocumentElastic documentOriginal, string newId, object document);
        Result ValidateAttachmentFields(object document, IEnumerable<string> attachmentFields);
        Result ValidateDocument(string dataSetName, object document);
        Result ValidateUpdateDocument(string dataSetName, object document);
        Result ValidateExtraFields(JToken token, IEnumerable<string> documentFields);
        Result ValidateFieldFilterFields(string dataSetName, List<string> fields);
        Result ValidateIdField(JToken root, string idField);
        Result ValidateTagField(JToken root, string tagField, bool? tagIsArray, bool? tagIsInteger, bool sampleDocument);
        Result ValidateInterpretedFields(JToken root, IEnumerable<string> interpretedFields);
        Result ValidateSampleDocument(DataSet dataSet);
        Result ValidateSchema(DataSet dataSet);
        Result ValidateSchema(JToken root, string idField, string tagField, IEnumerable<string> interpretedFields);
        Result ValidateSchemaIdField(JToken root, string idField);
        Result ValidateSchemaTagField(JToken root, string tagField);
        Result ValidateSchemaInterpretedFields(JToken root, IEnumerable<string> interpretedFields);
        Result ValidateOrderByField(string dataSetName, string orderByField);
        PaginatedList<object> GetScrolled(string dataSetName, string scrollId);

        BulkResults Bulk(string dataSetName, IEnumerable<object> documents, long requestSize, int parallelLimit);

        string GetIdValue(string dataSetName, object document);
        object GetFieldValue(object document, string field);
        Process StartCopyOrMove(string dataSetName, IDocumentSettings settings, bool isMove, int parallelLimit = -1);
        void CopyOrMove(string processId, string dataSetName, IEnumerable<string> documentIds, string targetDataSetName, int parallelLimit, bool isMove, CancellationToken token, string hostUrl);

        PaginatedList<object> Sample(string dataSetName, string seed, IEnumerable<string> tagIds, int size, IEnumerable<string> fields = null);
        PaginatedList<object> Sample(string dataSetName, string seed, IEnumerable<string> tagIds, double percent, IEnumerable<string> fields = null);
    }
}