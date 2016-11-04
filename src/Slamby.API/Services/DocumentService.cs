using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using Slamby.API.Helpers;
using Slamby.API.Models;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Slamby.Common;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MoreLinq;
using Slamby.Common.Config;

namespace Slamby.API.Services
{
    [TransientDependency(ServiceType = typeof(IDocumentService))]
    public class DocumentService : IDocumentService
    {
        readonly IGlobalStoreManager globalStore;
        readonly IQueryFactory queryFactory;
        private const string _allFieldCharacter = "*";

        GlobalStoreDataSet DataSet(string dataSetName) => globalStore.DataSets.Get(dataSetName);
        IDocumentQuery DocumentQuery(string dataSetName) => queryFactory.GetDocumentQuery(dataSetName);

        readonly SiteConfig siteConfig;

        public DocumentService(IGlobalStoreManager globalStore, IQueryFactory queryFactory, SiteConfig siteConfig)
        {
            this.siteConfig = siteConfig;
            this.queryFactory = queryFactory;
            this.globalStore = globalStore;
        }
        
        private ScrolledSearchResult<DocumentElastic> FilterInternal(
            GlobalStoreDataSet dataSet,
            string generalQuery, List<string> tagIds, int limit, string orderBy,
            bool isDescending, List<string> documentObjectFields)
        {
            var searchResult = DocumentQuery(dataSet.IndexName)
                .Filter(
                    generalQuery,
                    tagIds,
                    dataSet.DataSet.TagField,
                    limit,
                    orderBy,
                    isDescending,
                    dataSet.DataSet.InterpretedFields,
                    dataSet.DocumentFields,
                    documentObjectFields);

            return searchResult;
        }

        public PaginatedList<object> Filter(string dataSetName, string generalQuery, List<string> tagIds, int limit, string orderBy, 
            bool isDescending, List<string> documentObjectFields = null)
        {
            var dataSet = DataSet(dataSetName);
            var documentElasticFields = GetFieldFilter(dataSet, documentObjectFields);
            var searchResult = FilterInternal(
                dataSet,
                generalQuery,
                tagIds,
                limit,
                orderBy,
                isDescending,
                documentElasticFields);

            return UnwrapDocuments(searchResult);
        }

        public static List<string> GetFieldFilter(GlobalStoreDataSet dataSet, IEnumerable<string> fields)
        {
            var fieldList = new List<string>(fields ?? new string[] { });
            if (!fieldList.Any())
            {
                fieldList.Add(dataSet.DataSet.IdField);
                fieldList.Add(dataSet.DataSet.TagField);
                fieldList.AddRange(dataSet.DataSet.InterpretedFields);
                
                // Remove attachment type fields
                fieldList = fieldList.Except(dataSet.AttachmentFields).ToList();
            }
            else if (fieldList.Contains(_allFieldCharacter))
            {
                fieldList.Clear();
                fieldList.AddRange(dataSet.DocumentFields);
            }

            return fieldList;
        }

        public Result ValidateSampleDocument(DataSet dataSet)
        {
            var token = JTokenHelper.GetToken(dataSet.SampleDocument);
            return ValidateSampleDocument(token, dataSet.IdField, dataSet.TagField, dataSet.InterpretedFields);
        }
        public Result ValidateSampleDocument(JToken root, string idField, string tagField, IEnumerable<string> interpretedFields)
        {
            return Result.Combine(
                () => ValidateFields(root),
                () => ValidateIdField(root, idField),
                () => ValidateTagField(root, tagField, null, null, true),
                () => ValidateInterpretedFields(root, interpretedFields));
        }

        public Result ValidateDocument(string dataSetName, object document)
        {
            var dataSet = DataSet(dataSetName);
            return ValidateDocument(
                JTokenHelper.GetToken(document),
                dataSet.DataSet.IdField,
                dataSet.DataSet.TagField,
                dataSet.TagIsArray,
                dataSet.TagIsInteger,
                dataSet.DataSet.InterpretedFields,
                dataSet.AttachmentFields,
                dataSet.DocumentFields);
        }

        public Result ValidateUpdateDocument(string dataSetName, object document)
        {
            var dataSet = DataSet(dataSetName);
            return ValidateUpdateDocument(
                JTokenHelper.GetToken(document),
                dataSet.DataSet.IdField,
                dataSet.DataSet.TagField,
                dataSet.TagIsArray,
                dataSet.TagIsInteger,
                dataSet.DataSet.InterpretedFields,
                dataSet.AttachmentFields,
                dataSet.DocumentFields);
        }

        private Result ValidateDocument(JToken root, string idField, string tagField, bool tagIsArray, bool tagIsInteger,
            IEnumerable<string> interpretedFields, IEnumerable<string> attachmentFields,
            IEnumerable<string> documentFields)
        {
            return Result.Combine(
                () => ValidateFields(root),
                () => ValidateIdField(root, idField),
                () => ValidateTagField(root, tagField, tagIsArray, tagIsInteger, false),
                () => ValidateInterpretedFields(root, interpretedFields),
                () => ValidateAttachmentFields(root, attachmentFields),
                () => ValidateExtraFields(root, documentFields));
        }

        private Result ValidateUpdateDocument(JToken root, string idField, string tagField, bool tagIsArray, bool tagIsInteger,
            IEnumerable<string> interpretedFields, IEnumerable<string> attachmentFields,
            IEnumerable<string> documentFields)
        {
            var actionResult = new List<Func<Result>>();

            if (root.GetPathToken(idField) != null) { actionResult.Add(() => ValidateIdField(root, idField));  }
            if (root.GetPathToken(tagField) != null) { actionResult.Add(() => ValidateTagField(root, tagField, tagIsArray, tagIsInteger, false)); }

            var existingInterpretedFields = interpretedFields.Where(field => root.GetPathToken(field) != null).ToList();
            if (existingInterpretedFields.Any()) actionResult.Add(() => ValidateInterpretedFields(root, existingInterpretedFields));

            var existingAttachmentFields = attachmentFields.Where(field => root.GetPathToken(field) != null).ToList();
            if (existingAttachmentFields.Any()) actionResult.Add(() => ValidateAttachmentFields(root, existingAttachmentFields));

            var existingDocumentFields = documentFields.Where(field => root.GetPathToken(field) != null).ToList();
            if (existingDocumentFields.Any()) actionResult.Add(() => ValidateExtraFields(root, existingDocumentFields));

            return Result.Combine(actionResult.ToArray());
        }

        /// <summary>
        /// A field neveknek meg kell felelniük bizonyos feltételeknek
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        public Result ValidateFields(JToken root)
        {
            var fields = DocumentHelper.GetAllPropertyNames(root);
            var rgx = new Regex(SDK.Net.Constants.ValidationCommonRegex);
            foreach (var field in fields)
            {
                if (!rgx.IsMatch(field))
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_MustMatchRegex_2, "SampleDocument", field, SDK.Net.Constants.ValidationCommonRegex));
                }
                if (field.Length > SDK.Net.Constants.ValidationCommonMaximumLength)
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_MustBeMin_2_Max_3, "SampleDocument", field, 1, SDK.Net.Constants.ValidationCommonMaximumLength));
                }
                if (field.Length < 1)
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_MustBeMin_2_Max_3, "SampleDocument", field, 1, SDK.Net.Constants.ValidationCommonMaximumLength));
                }
            }
            return Result.Ok();
        }

        /// <summary>
        /// Az interpretedField csak egyszerű típus lehet
        /// </summary>
        /// <param name="root"></param>
        /// <param name="interpretedFields"></param>
        /// <returns></returns>
        public Result ValidateInterpretedFields(JToken root, IEnumerable<string> interpretedFields)
        {
            foreach (var interpretedField in interpretedFields)
            {   
                var fieldToken = root.GetPathToken(interpretedField);
                if (fieldToken == null)
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotFound, "Interpreted", interpretedField));
                }

                var fieldValue = fieldToken as JValue;
                if (fieldValue == null || !fieldValue.IsStringOrInteger())
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotSimpleType, "Interpreted", interpretedField));
                }
            }

            return Result.Ok();
        }

        /// <summary>
        /// A tag csak egyszerű vagy array típus lehet
        /// </summary>
        /// <param name="root"></param>
        /// <param name="tagField"></param>
        /// <returns></returns>
        public Result ValidateTagField(JToken root, string tagField, bool? tagIsArray, bool? tagIsInteger, bool sampleDocument)
        {
            var tagToken = root.GetPathToken(tagField);
            if (tagToken == null)
            {
                return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotFound, "Tag", tagField));
            }
            if (tagToken is JObject)
            {
                return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsAnObjectType, "Tag", tagField));
            }

            var tagValue = tagToken as JValue;
            if (tagValue != null)
            {
                // null is allowed, cannot test further things
                if (!sampleDocument && tagValue.Value == null)
                {
                    return Result.Ok();
                }

                if (tagIsArray == true)
                {
                    return Result.Fail(DocumentResources.TheFieldIsDefinedAsArrayButItIsSimpleType);
                }
                if (!tagValue.IsStringOrInteger())
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotSimpleType, "Tag", tagField));
                }
                if (tagIsInteger.HasValue && tagIsInteger.Value && !tagValue.IsInteger())
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotInteger, "Tag", tagField));
                }

                if (sampleDocument)
                {
                    if (string.IsNullOrWhiteSpace(tagValue.Value.ToString()))
                    {
                        return Result.Fail(string.Format(DocumentResources.The_0_Field_1_HasNoContent, "Tag", tagField));
                    }
                }
            }

            var tagArray = tagToken as JArray;
            if (tagArray != null)
            {
                if (tagIsArray == false)
                {
                    return Result.Fail(DocumentResources.TheFieldIsDefinedAsSimpleTypeButItIsArray);
                }

                var elementToken = tagArray.GetUnderlyingToken();
                if (sampleDocument)
                {
                    if (elementToken == null)
                    {
                        return Result.Fail(string.Format(DocumentResources.The_0_Field_1_ArrayHasNoItems, "Tag", tagField));
                    }
                }

                if (elementToken != null)
                {
                    if (!elementToken.IsStringOrInteger())
                    {
                        return Result.Fail(string.Format(DocumentResources.The_0_Field_1_ArrayItemIsNotSimpleType, "Tag", tagField));
                    }
                    if (tagIsInteger.HasValue && tagIsInteger.Value && !elementToken.IsInteger())
                    {
                        return Result.Fail(string.Format(DocumentResources.The_0_Field_1_ArrayItemIsNotInteger, "Tag", tagField));
                    }
                }

                if (sampleDocument)
                {
                    var elementValue = elementToken as JValue;
                    if (elementValue == null || string.IsNullOrWhiteSpace(elementValue.Value.ToString()))
                    {
                        return Result.Fail(string.Format(DocumentResources.The_0_Field_1_ArrayItemIsHasNoContent, "Tag", tagField));
                    }
                }
            }

            return Result.Ok();
        }

        /// <summary>
        /// Document Id field validation
        /// Az idField csak egyszerű típus lehet és kell értéke legyen
        /// </summary>
        /// <param name="root"></param>
        /// <param name="idField"></param>
        /// <returns></returns>
        public Result ValidateIdField(JToken root, string idField)
        {   
            var idToken = root.GetPathToken(idField);
            if (idToken == null)
            {
                return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotFound, "Id", idField));
            }

            var idValue = idToken as JValue;
            if (idValue == null)
            {
                return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotSimpleType, "Id", idField));
            }
            if (string.IsNullOrWhiteSpace(idValue.Value.ToString()))
            {
                return Result.Fail(string.Format(DocumentResources.The_0_Field_1_HasNoContent, "Id", idField));
            }

            return Result.Ok();
        }

        public Result ValidateAttachmentFields(object document, IEnumerable<string> attachmentFields)
        {
            foreach (var field in attachmentFields)
            {
                var base64 = DocumentHelper.GetValue(document, field)?.ToString();
                if (string.IsNullOrEmpty(base64))
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_HasNoContent, "attachment", field));
                }

                base64 = base64.CleanBase64();

                if (!base64.IsBase64())
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotBase64, "attachment", field));
                }
            }

            return Result.Ok();
        }

        public Result ValidateExtraFields(JToken token, IEnumerable<string> documentFields)
        {
            var extraFields = DocumentHelper.GetExtraFields(token, documentFields);
            if (extraFields.Any())
            {
                return Result.Fail(string.Format(DocumentResources.InvalidDocumentField_0, string.Join(", ", extraFields)));
            }

            return Result.Ok();
        }

        public Result ValidateSchema(DataSet dataSet)
        {
            var token = JTokenHelper.GetToken(dataSet.Schema);
            return ValidateSchema(token, dataSet.IdField, dataSet.TagField, dataSet.InterpretedFields);
        }

        public Result ValidateSchema(JToken root, string idField, string tagField, IEnumerable<string> interpretedFields)
        {
            return Result.Combine(
                () => ValidateSchemaIdField(root, idField),
                () => ValidateSchemaTagField(root, tagField),
                () => ValidateSchemaInterpretedFields(root, interpretedFields));
        }

        public Result ValidateSchemaIdField(JToken root, string idField)
        {
            var paths = SchemaHelper.GetPaths(root);

            if (!paths.ContainsKey(idField))
            {
                return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotFound, "Id", idField));
            }
            if (!SchemaHelper.IsPrimitiveType(paths[idField].Item1))
            {
                return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotSimpleType, "Id", idField));
            }

            return Result.Ok();
        }

        public Result ValidateSchemaTagField(JToken root, string tagField)
        {
            var paths = SchemaHelper.GetPaths(root);
            if (!paths.ContainsKey(tagField))
            {
                return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotFound, "Tag", tagField));
            }

            // a tag csak egyszerű vagy array típus lehet
            if (SchemaHelper.IsObject(paths[tagField].Item1))
            {
                return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsAnObjectType, "Tag", tagField));
            }

            if (SchemaHelper.IsAttachment(paths[tagField].Item1))
            {
                return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotSimpleType, "Tag", tagField));
            }

            if (SchemaHelper.IsArray(paths[tagField].Item1))
            {
                if (!SchemaHelper.IsPrimitiveType(paths[tagField].Item2))
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_ArrayItemIsNotSimpleType, "Tag", tagField));
                }
            }

            return Result.Ok();
        }

        public Result ValidateSchemaInterpretedFields(JToken root, IEnumerable<string> interpretedFields)
        {
            var paths = SchemaHelper.GetPaths(root);

            foreach (var interpretedField in interpretedFields)
            {
                if (!paths.ContainsKey(interpretedField))
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotFound, "Interpreted", interpretedField));
                }

                // Interpreted field cannot be `object`
                if (!SchemaHelper.IsPrimitiveType(paths[interpretedField].Item1))
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_Field_1_IsNotSimpleType, "Interpreted", interpretedField));
                }
            }

            return Result.Ok();
        }

        /// <summary>
        /// Cleans documents from non-existing tags.
        /// </summary>
        public void CleanDocuments(string dataSetName, List<string> tagIds)
        {
            var documentQuery = DocumentQuery(dataSetName);
            var dataSet = DataSet(dataSetName);
            var documentElastics = documentQuery.GetTagIdFieldOnly(dataSet.DataSet.TagField);
            var documentTagIds = GetTagIds(documentElastics.Items, dataSet.DataSet.TagField);
            var tagIdsToRemove = documentTagIds.Except(tagIds, StringComparer.OrdinalIgnoreCase).ToList();

            if (!tagIdsToRemove.Any())
            {
                return;
            }

            var pageSize = 1000;
            var scrolledResults = FilterInternal(dataSet, null, tagIdsToRemove, pageSize, dataSet.DataSet.IdField, false, new List<string> { dataSet.DataSet.TagField });

            while(scrolledResults.Items.Any())
            {
                foreach (var documentElastic in scrolledResults.Items)
                {
                    DocumentHelper.RemoveTagIds(documentElastic.DocumentObject, dataSet.DataSet.TagField, tagIdsToRemove);
                    documentQuery.Update(documentElastic.Id, documentElastic);
                }

                scrolledResults = documentQuery.GetScrolled(scrolledResults.ScrollId);
            }

            documentQuery.Optimize();
        }

        public HashSet<string> GetTagIds(IEnumerable<DocumentElastic> documents, string tagField)
        {
            if (documents == null)
            {
                throw new ArgumentNullException(nameof(documents));
            }
            if (string.IsNullOrEmpty(tagField))
            {
                throw new ArgumentException(nameof(tagField));
            }

            // HashSet stores unique items
            var documentTagIds = new HashSet<string>();
            foreach (var documentElastic in documents.Where(d => d.DocumentObject != null))
            {
                var tags = DocumentHelper.GetValue(documentElastic.DocumentObject, tagField);
                if (tags is IEnumerable)
                {
                    foreach (var tag in (IEnumerable)tags)
                    {
                        documentTagIds.Add(tag.ToString());
                    }
                }
                else
                {
                    documentTagIds.Add(tags.ToString());
                }
            }

            return documentTagIds;
        }

        public HashSet<string> GetTagIds(DocumentElastic document, string fieldName)
        {
            return GetTagIds(new[] { document }, fieldName);
        }

        public DocumentElastic Get(string dataSetName, string id)
        {
            return DocumentQuery(dataSetName).Get(id);
        }

        public bool IsExists(string dataSetName, string id)
        {
            return DocumentQuery(dataSetName).IsExists(id);
        }

        public Result Update(string dataSetName, string id, DocumentElastic documentOriginal, string newId, object document)
        {
            var documentQuery = DocumentQuery(dataSetName);
            if (!string.Equals(id, newId, StringComparison.Ordinal))
            {
                var documentReindexed = Get(dataSetName, id);
                documentReindexed.Id = newId;
                documentReindexed.ModifiedDate = DateTime.UtcNow;

                Delete(dataSetName, id);
                var response = Index(dataSetName, documentReindexed);
                if (response.ItemsWithErrors?.Count > 0)
                {
                    // mivel már törölve lett az eredeti, megpróbáljuk újraindexelni 
                    Index(dataSetName, documentOriginal);
                    return Result.Fail(response.ItemsWithErrors.Select(s => s.Error.Reason).First());
                }
            }

            var documentModified = Get(dataSetName, newId);

            documentModified.DocumentObject = document;
            documentModified.ModifiedDate = DateTime.UtcNow;

            documentModified.Text = DocumentHelper.GetConcatenatedText(document, DataSet(dataSetName).DataSet.InterpretedFields, documentOriginal.DocumentObject);

            documentQuery.Update(newId, documentModified);

            return Result.Ok();
        }

        public void Delete(string dataSetName, string id)
        {
            DocumentQuery(dataSetName).Delete(id);
        }

        public Result Index(string dataSetName, object document, string id)
        {
            var documentElastic = new DocumentElastic
            {
                Id = id,
                DocumentObject = document,
                Text = DocumentHelper.GetConcatenatedText(document, DataSet(dataSetName).DataSet.InterpretedFields)
            };

            var response = DocumentQuery(dataSetName).Index(documentElastic);
            if (response.ItemsWithErrors?.Count > 0)
            {
                return Result.Fail(response.ItemsWithErrors.Select(s => s.Error.Reason).First());
            }

            return Result.Ok();
        }

        public NestBulkResponse Index(string dataSetName, DocumentElastic documentElastic)
        {
            documentElastic.Text = DocumentHelper.GetConcatenatedText(documentElastic.DocumentObject, DataSet(dataSetName).DataSet.InterpretedFields);
            return DocumentQuery(dataSetName).Index(documentElastic);
        }

        public Result ValidateFieldFilterFields(string dataSetName, List<string> fields)
        {
            if (fields == null || !fields.Any()) return Result.Ok();
            var dataSet = DataSet(dataSetName).DataSet;
            foreach (var field in fields)
            {
                if (field == _allFieldCharacter) continue;

                if (dataSet.SampleDocument != null &&
                    !DocumentHelper.IsExist(dataSet.SampleDocument, field))
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_FieldIsMissing, field));
                }
                if (dataSet.Schema != null)
                {
                    var schemaFields = SchemaHelper.GetPaths(dataSet.Schema);
                    if (!schemaFields.ContainsKey(field))
                    {
                        return Result.Fail(string.Format(DocumentResources.The_0_FieldIsMissing, field));
                    }
                }
            }
            return Result.Ok();
        }


        public Result ValidateOrderByField(string dataSetName, string orderByField)
        {
            var dataSet = DataSet(dataSetName).DataSet;
            if (dataSet.SampleDocument != null)
            {
                if (!DocumentHelper.IsExist(dataSet.SampleDocument, orderByField))
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_FieldIsMissing, orderByField));
                }
                if (!DocumentHelper.IsPrimitiveType(dataSet.SampleDocument, orderByField))
                {
                    return Result.Fail(DocumentResources.OnlyFieldOfPrimitiveTypeIsAllowedForOrderByField);
                }
            }
            if (dataSet.Schema != null)
            {
                var schemaFields = SchemaHelper.GetPaths(dataSet.Schema);
                if (!schemaFields.ContainsKey(orderByField))
                {
                    return Result.Fail(string.Format(DocumentResources.The_0_FieldIsMissing, orderByField));
                }

                var orderFieldType = schemaFields[orderByField];
                if (!SchemaHelper.IsPrimitiveType(orderFieldType.Item1))
                {
                    return Result.Fail(DocumentResources.OnlyFieldOfPrimitiveTypeIsAllowedForOrderByField);
                }
            }

            return Result.Ok();
        }

        public PaginatedList<object> GetScrolled(string dataSetName, string scrollId)
        {
            return UnwrapDocuments(DocumentQuery(dataSetName).GetScrolled(scrollId));
        }

        private PaginatedList<object> UnwrapDocuments(ScrolledSearchResult<DocumentElastic> searchResult)
        {
            var items = searchResult.Items
                .Where(d => d != null)
                .Select(d => d.DocumentObject)
                .ToList();

            var paginatedList = new PaginatedList<object>
            {
                Items = items,
                Total = Convert.ToInt32(searchResult.Total),
                Count = items.Count,
                ScrollId = searchResult.ScrollId
            };

            return paginatedList;
        }

        private PaginatedList<object> UnwrapDocuments(SearchResult<DocumentElastic> searchResult)
        {
            var items = searchResult.Items
                .Where(d => d != null)
                .Select(d => d.DocumentObject)
                .ToList();

            var paginatedList = new PaginatedList<object>
            {
                Items = items,
                Total = Convert.ToInt32(searchResult.Total),
                Count = items.Count,
                ScrollId = null
            };

            return paginatedList;
        }

        public BulkResults Bulk(string dataSetName, IEnumerable<object> documents, long requestSize, int parallelLimit)
        {
            var dataSet = DataSet(dataSetName).DataSet;
            var results = new BulkResults();
            var validatedDocuments = documents
                .Select((document, index) =>
                    new
                    {
                        Index = index,
                        Result = ValidateDocument(dataSetName, document),
                        Document = document
                    })
                    .ToList();

            var invalidDocumentResults = validatedDocuments
                .Where(document => document.Result.IsFailure)
                .Select(document => BulkResult.Create(
                    DocumentHelper.GetValue(document.Document, dataSet.IdField)?.ToString(),
                    StatusCodes.Status400BadRequest,
                    document.Result.Error)
                    )
                .ToList();

            results.Results.AddRange(invalidDocumentResults);

            var documentElastics = validatedDocuments
                    .Where(obj => obj.Result.IsSuccess)
                    .Select(document => new DocumentElastic
                    {
                        Id = DocumentHelper.GetValue(document.Document, dataSet.IdField).ToString(),
                        DocumentObject = document.Document,
                        Text = DocumentHelper.GetConcatenatedText(document.Document, dataSet.InterpretedFields)
                    })
                    .ToList();

            var bulkResponseStruct = DocumentQuery(dataSetName).ParallelBulkIndex(documentElastics, parallelLimit, requestSize);
            results.Results.AddRange(bulkResponseStruct.ToBulkResult());

            return results;
        }

        public string GetIdValue(string dataSetName, object document)
        {
            var dataSet = DataSet(dataSetName).DataSet;
            return GetFieldValue(document, dataSet.IdField)?.ToString();
        }

        public object GetFieldValue(object document, string field)
        {
            return DocumentHelper.GetValue(document, field);
        }

        public Result Copy(string dataSetName, IEnumerable<string> documentIds, string targetDataSetName, int parallelLimit = -1)
        {
            //// TODO: Validate target schema
            var copiedDocumentIds = new ConcurrentBag<string>();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parallelLimit };
            var sourceDocumentQuery = DocumentQuery(dataSetName);
            var targetDocumentQuery = DocumentQuery(targetDataSetName);

            var isCompleted = Parallel.ForEach(
                documentIds.Batch(siteConfig.Resources.MaxSearchBulkCount), 
                parallelOptions, 
                (batchIds, loopState) =>
            {
                try
                {
                    var batchDocuments = sourceDocumentQuery.Get(batchIds);
                    var interpretedFields = DataSet(dataSetName).DataSet.InterpretedFields;

                    foreach (var document in batchDocuments.Where(doc => string.IsNullOrEmpty(doc.Text)))
                    {
                        document.Text = DocumentHelper.GetConcatenatedText(document.DocumentObject, interpretedFields);
                    }

                    var bulkResponse = targetDocumentQuery.Index(batchDocuments);
                    var returnedIds = bulkResponse.Items.Select(i => i.Id).ToList();

                    returnedIds.ForEach(id => copiedDocumentIds.Add(id));

                    if (batchIds.Except(returnedIds).Count() > 0)
                    {
                        loopState.Stop();
                    }
                }
                catch
                {
                    loopState.Stop();
                }
            }).IsCompleted;

            if (!isCompleted)
            {
                targetDocumentQuery.Delete(copiedDocumentIds);
                return Result.Fail(DocumentResources.ErrorDuringIndexingDocuments);
            }

            targetDocumentQuery.Flush();

            return Result.Ok();
        }

        public Result Move(string dataSetName, IEnumerable<string> documentIds, string targetDataSetName, int parallelLimit = -1)
        {
            // TODO: Validate target schema
            var result = Copy(dataSetName, documentIds, targetDataSetName, parallelLimit);
            if (result.IsFailure)
            {
                return result;
            }

            var sourceDocumentQuery = DocumentQuery(dataSetName);

            sourceDocumentQuery.Delete(documentIds);
            sourceDocumentQuery.Flush();

            return Result.Ok();
        }

        public PaginatedList<object> Sample(string dataSetName, string seed, IEnumerable<string> tagIds, int size, IEnumerable<string> fields = null)
        {
            var dataSet = DataSet(dataSetName);
            var result = DocumentQuery(dataSetName).Sample(seed, tagIds, dataSet.DataSet.TagField, size, GetFieldFilter(dataSet, fields));

            return UnwrapDocuments(result);
        }

        public PaginatedList<object> Sample(string dataSetName, string seed, IEnumerable<string> tagIds, double percent, IEnumerable<string> fields = null)
        {
            var dataSet = DataSet(dataSetName);
            var result = DocumentQuery(dataSetName).Sample(seed, tagIds, dataSet.DataSet.TagField, percent, GetFieldFilter(dataSet, fields));

            return UnwrapDocuments(result);
        }
    }
}