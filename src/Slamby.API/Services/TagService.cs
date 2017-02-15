using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Slamby.API.Helpers;
using Slamby.API.Models;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Helpers;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models;
using Slamby.SDK.Net.Models.Enums;

namespace Slamby.API.Services
{
    [TransientDependency]
    public class TagService
    {
        readonly IGlobalStoreManager globalStore;

        readonly IQueryFactory queryFactory;
        readonly ProcessHandler processHandler;
        readonly TagsHandler tagsHandler;
        readonly UrlProvider urlProvider;
        readonly IDocumentService documentService;

        public TagService(IQueryFactory queryFactory,
            ProcessHandler processHandler, TagsHandler tagsHandler, UrlProvider urlProvider, 
            IDocumentService documentService, IGlobalStoreManager globalStore)
        {
            this.globalStore = globalStore;
            this.documentService = documentService;
            this.urlProvider = urlProvider;
            this.tagsHandler = tagsHandler;
            this.processHandler = processHandler;
            this.queryFactory = queryFactory;
        }

        private GlobalStoreDataSet DataSet(string dataSetName)
        {
            return globalStore.DataSets.Get(dataSetName);
        }
        private TagQuery TagQuery(string dataSetName)
        {
            return queryFactory.GetTagQuery(dataSetName);
        }
        private IDocumentQuery DocumentQuery(string dataSetName)
        {
            return queryFactory.GetDocumentQuery(dataSetName);
        }

        /// <summary>
        /// Gets the tags.
        /// </summary>
        /// <param name="withDetails">if set to <c>true</c> [with details].</param>
        /// <returns></returns>
        public List<Tag> GetTagModels(string dataSetName, bool withDetails)
        {
            var dataSet = DataSet(dataSetName).DataSet;

            var tags = new List<Tag>();
            var tagElastics = TagQuery(dataSetName).GetAll().Items;
            var tagElasticsDic = tagElastics.ToDictionary(t => t.Id, t => t);
            var wordQuery = queryFactory.GetWordQuery(dataSet.Name);

            Dictionary<string, int> docCountDic = null;
            Dictionary<string, int> wordCountDic = null;

            if (withDetails)
            {
                docCountDic = DocumentQuery(dataSetName).CountForTags(tagElasticsDic.Keys.ToList(), dataSet.TagField);
                // the text field contains the concatenate word also, so we have to use the interpretedfields
                wordCountDic = wordQuery.CountForWord(dataSet.InterpretedFields.Select(Elastic.Queries.DocumentQuery.MapDocumentObjectName).ToList(), 1, tagElasticsDic.Keys.ToList(), Elastic.Queries.DocumentQuery.MapDocumentObjectName(dataSet.TagField));
            }

            foreach (var tagId in tagElasticsDic.Keys)
            {
                var tagElastic = tagElasticsDic[tagId];
                var pathItems = tagElasticsDic
                    .Where(d => tagElastic.ParentIdList.Contains(d.Key))
                    .OrderBy(t => t.Value.Level)
                    .Select(d => new PathItem { Id = d.Value.Id, Name = d.Value.Name, Level = d.Value.Level })
                    .ToList();
                var wordCount = 0;
                var documentCount = 0;

                if (withDetails)
                {
                    documentCount = docCountDic[tagId];
                    wordCount = wordCountDic[tagId];
                }

                var tag = new Tag
                {
                    Id = tagElastic.Id,
                    Name = tagElastic.Name,
                    ParentId = tagElastic.ParentId(),
                    Properties = new TagProperties
                    {
                        Paths = pathItems,
                        IsLeaf = tagElastic.IsLeaf,
                        Level = tagElastic.Level,
                        DocumentCount = documentCount,
                        WordCount = wordCount
                    }
                };
                tags.Add(tag);
            }

            if (withDetails)
            {
                var tagIdsByLevel = TagHelper.GetTagIdsByLevel(tags, p => p.ParentId, i => i.Id);

                // Go leaf to root to add child document & word count to parent properties
                if (tagIdsByLevel.Keys.Count > 1)
                {
                    for (int level = tagIdsByLevel.Keys.Count - 2; level > 0; level--)
                    {
                        foreach (var tagIdByLevel in tagIdsByLevel[level])
                        {
                            var tag = tags.FirstOrDefault(t => t.Id == tagIdByLevel);
                            if (tag == null)
                            {
                                continue;
                            }

                            var childTags = tags.Where(t => t.ParentId == tagIdByLevel).ToList();
                            tag.Properties.DocumentCount += childTags.Sum(t => t.Properties.DocumentCount);
                            tag.Properties.WordCount += childTags.Sum(t => t.Properties.WordCount);
                        }
                    }
                }
            }

            return tags;
        }

        /// <summary>
        /// Gets the tag.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="withDetails">if set to <c>true</c> [with details].</param>
        /// <param name="tagElasticsDic">If don't want to work from database, then the tags can be given</param>
        /// <returns></returns>
        public Tag GetTagModel(string dataSetName, string id, bool withDetails, Dictionary<string, TagElastic> tagElasticsDic = null)
        {
            var dataSet = DataSet(dataSetName).DataSet;
            var tagQuery = TagQuery(dataSetName);
            var tagElastic = tagElasticsDic != null ? tagElasticsDic[id] : tagQuery.Get(id);
            var pathItems = new List<PathItem>();

            if (tagElastic.ParentIdList.Any())
            {
                IEnumerable<TagElastic> pathTagElastics = new List<TagElastic>();
                if (tagElasticsDic != null)
                {
                    pathTagElastics = tagElastic.ParentIdList.Where(pid => tagElasticsDic.ContainsKey(pid)).Select(pid => tagElasticsDic[pid]);
                } else
                {
                    pathTagElastics = tagQuery.Get(tagElastic.ParentIdList);
                }
                
                pathItems = pathTagElastics
                            .OrderBy(t => t.Level)
                            .Select(t => new PathItem { Id = t.Id, Name = t.Name, Level = t.Level })
                            .ToList();
            }

            var tag = new Tag
            {
                Id = tagElastic.Id,
                Name = tagElastic.Name,
                ParentId = tagElastic.ParentId(),
                Properties = new TagProperties
                {
                    Paths = pathItems,
                    IsLeaf = tagElastic.IsLeaf,
                    Level = tagElastic.Level
                }
            };

            if (withDetails)
            {
                var wordQuery = queryFactory.GetWordQuery(dataSet.Name);
                var descendantTagIds = tagQuery.GetDescendantTagIds(tagElastic.Id);
                var descendantTagIdsWithParentId = descendantTagIds.Concat(new[] { tagElastic.Id }).ToList();
                var countDic = DocumentQuery(dataSetName).CountForTags(descendantTagIdsWithParentId, dataSet.TagField);

                var documentCount = descendantTagIdsWithParentId
                    .Select(tagId => countDic[tagId])
                    .Sum();
                // the text field contains the concatenate word also, so we have to use the interpretedfields
                var wordCount = wordQuery.CountForWord(dataSet.InterpretedFields.Select(Elastic.Queries.DocumentQuery.MapDocumentObjectName).ToList(), 1, descendantTagIdsWithParentId, dataSet.TagField)
                    .Sum(item => item.Value);

                tag.Properties.DocumentCount = documentCount;
                tag.Properties.WordCount = wordCount;
            }

            return tag;
        }

        /// <summary>
        /// Determines whether the specified identifier is exist.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        public bool IsExist(string dataSetName, string id)
        {
            return TagQuery(dataSetName).Get(id) != null;
        }

        /// <summary>
        /// Validates the tag id.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        public Result ValidateTagId(string dataSetName, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Result.Fail(TagResources.TagIdIsEmpty);
            }

            var tagIsInteger = DataSet(dataSetName).TagIsInteger;

            if (tagIsInteger)
            {
                if (id.Any(ch => !char.IsDigit(ch)))
                {
                    return Result.Fail(TagResources.TagIdShouldBeIntegerType);
                }
            }

            return Result.Ok();
        }

        /// <summary>
        /// Validates the parent tag.
        /// </summary>
        /// <param name="parentId">The parent identifier.</param>
        /// <returns></returns>
        public bool ValidateParentTag(string dataSetName, string parentId)
        {
            if (!string.IsNullOrWhiteSpace(parentId) &&
                TagQuery(dataSetName).Get(parentId) == null)
            {
                return false;
            }

            return true;
        }

        public bool HasChildren(string dataSetName, string id)
        {
            return TagQuery(dataSetName).GetDescendantTagIds(id).Any();
        }

        /// <summary>
        /// Deletes the specified tag.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="force">if set to <c>true</c> [force].</param>
        /// <param name="cleanDocuments">if set to <c>true</c> [clean documents].</param>
        /// <returns></returns>
        public void Delete(string dataSetName, string id, bool force, bool cleanDocuments)
        {
            var dataSet = DataSet(dataSetName).DataSet;
            var result = new BulkResults();
            var tagQuery = TagQuery(dataSetName);
            var documentQuery = DocumentQuery(dataSetName);
            var tagElastic = tagQuery.Get(id);
            var childTagIds = tagQuery.GetDescendantTagIds(id);

            TagElastic parentTagElastic = null;

            if (!string.IsNullOrWhiteSpace(tagElastic.ParentId()))
            {
                parentTagElastic = tagQuery.Get(tagElastic.ParentId());
            }

            if (cleanDocuments)
            {
                foreach (var childTagId in childTagIds)
                {
                    foreach (var documentElastic in documentQuery.GetByTagId(childTagId, dataSet.TagField))
                    {
                        DocumentHelper.RemoveTagIds(documentElastic.DocumentObject, dataSet.TagField, new List<string> { childTagId });
                        documentQuery.Index(documentElastic);
                    }
                }
            }

            var deleteTagIds = new List<string> { id };
            if (force)
            {
                deleteTagIds.AddRange(childTagIds);
            }

            tagQuery.DeleteByIds(deleteTagIds);
            tagQuery.AdjustLeafStatus(parentTagElastic);
        }

        /// <summary>
        /// Creates the specified tag.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <returns></returns>
        public Tag Create(string dataSetName, Tag tag)
        {
            var tagQuery = TagQuery(dataSetName);
            TagElastic parentTagElastic = tagQuery.Get(tag.ParentId);

            var level = 1;
            var parentIdList = new List<string>();

            if (parentTagElastic != null)
            {
                parentIdList.AddRange(parentTagElastic.ParentIdList);
                parentIdList.Add(parentTagElastic.Id);
                level = parentTagElastic.Level + 1;

                // If parent is currently leaf then set to false
                if (parentTagElastic.IsLeaf)
                {
                    parentTagElastic.IsLeaf = false;
                    tagQuery.Update(parentTagElastic.Id, parentTagElastic);
                }
            }

            var tagElastic = new TagElastic
            {
                Id = tag.Id,
                Name = tag.Name,
                ParentIdList = parentIdList,
                IsLeaf = true,
                Level = level
            };

            tagQuery.Index(tagElastic);

            return GetTagModel(dataSetName, tag.Id, false);
        }

        public TagElastic GetParentTag(string dataSetName, TagElastic tagElastic)
        {

            return !string.IsNullOrWhiteSpace(tagElastic.ParentId()) ? TagQuery(dataSetName).Get(tagElastic.ParentId()) : null;
        }

        /// <summary>
        /// Updates Tag with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="tag">The tag.</param>
        /// <returns></returns>
        public bool Update(string dataSetName, string id, Tag tag)
        {
            var tagQuery = TagQuery(dataSetName);
            var tagElastic = tagQuery.Get(id);
            TagElastic oldParentTagElastic = GetParentTag(dataSetName, tagElastic);
            TagElastic newParentTagElastic = null;

            if (!string.IsNullOrWhiteSpace(tag.ParentId))
            {
                newParentTagElastic = tagQuery.Get(tag.ParentId);

                if (newParentTagElastic == null)
                {
                    return false;
                }
            }

            var level = 1;
            var parentIdList = new List<string>();

            if (newParentTagElastic != null)
            {
                parentIdList.AddRange(newParentTagElastic.ParentIdList);
                parentIdList.Add(newParentTagElastic.Id);
                level = newParentTagElastic.Level + 1;

                // If parent is currently leaf then set to false
                if (newParentTagElastic.IsLeaf)
                {
                    newParentTagElastic.IsLeaf = false;
                    tagQuery.Update(newParentTagElastic.Id, newParentTagElastic);
                }
            }

            tagElastic.Id = tag.Id;
            tagElastic.Name = tag.Name;
            tagElastic.ModifiedDate = DateTime.UtcNow;
            tagElastic.Level = level;
            tagElastic.ParentIdList = parentIdList;

            tagQuery.Update(id, tagElastic);
            tagQuery.AdjustLeafStatus(oldParentTagElastic);

            return true;
        }

        /// <summary>
        /// Bulk tag creation.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        public Result<BulkResults> BulkCreate(string dataSetName, List<Tag> tags, int parallelLimit, long requestSize)
        {
            var results = new BulkResults();

            var tagIsInteger = DataSet(dataSetName).TagIsInteger;
            if (tagIsInteger && tags.Any(tag => tag.Id.Any(ch => !char.IsDigit(ch))))
            {
                results.Results = tags
                    .Where(tag => tag.Id.Any(ch => !char.IsDigit(ch)))
                    .Select(t => BulkResult.Create(
                        t.Id, 
                        StatusCodes.Status400BadRequest, 
                        string.Format(TagResources.TagIdShouldBeIntegerType, t.ParentId, t.Id)))
                    .ToList();

                return Result.Ok(results);
            }

            var tagIdsByLevel = TagHelper.GetTagIdsByLevel(tags, item => item.ParentId, item => item.Id);
            var validIds = tagIdsByLevel.SelectMany(l => l.Value).ToList();
            var invalidIds = tags.Select(t => t.Id).Except(validIds);

            if (invalidIds.Any())
            {
                results.Results = tags
                    .Where(t => invalidIds.Contains(t.Id))
                    .Select(t => BulkResult.Create(t.Id, StatusCodes.Status404NotFound, string.Format(TagResources.ParentId_0_NotFoundInTagWithId_1, t.ParentId, t.Id))).ToList();

                // returns with OK status, individual items contain error code
                return Result.Ok(results);
            }

            var orderedTagElasticList = tagIdsByLevel
                .SelectMany(dic => dic.Value)
                .Select(id =>
                {
                    var tag = tags.FirstOrDefault(t => t.Id == id);
                    var tagElastic = new TagElastic
                    {
                        Id = tag.Id,
                        Name = tag.Name,
                        ParentIdList = new List<string>()
                    };
                    if (!string.IsNullOrWhiteSpace(tag.ParentId))
                    {
                        tagElastic.ParentIdList.Add(tag.ParentId);
                    }
                    return tagElastic;
                })
                .ToList();

            TagHelper.AdjustTagElastics(orderedTagElasticList);

            var tagQuery = TagQuery(dataSetName);
            tagQuery.DeleteAll();

            var bulkResponseStruct = tagQuery.ParallelBulkIndex(orderedTagElasticList, parallelLimit, requestSize);

            results.Results.AddRange(bulkResponseStruct.ToBulkResult());

            return Result.Ok(results);
        }

        public Tag TrimTag(Tag tag)
        {
            tag.Id = tag.Id.SafeTrim();
            tag.Name = tag.Name.SafeTrim();
            tag.ParentId = tag.ParentId.SafeTrim();

            return tag;
        }

        public List<TagElastic> GetTagElastic(string dataSetName, List<string> tagIdList)
        {
            return TagQuery(dataSetName).Get(tagIdList).ToList();
        }

        public List<TagElastic> GetTagElasticLeafs(string dataSetName)
        {
            return TagQuery(dataSetName).GetAll().Items.Where(i => i.IsLeaf).ToList();
        }

        public Process ExportWords(string dataSetName, TagsExportWordsSettings settings, List<TagElastic> tags)
        {
            var process = processHandler.Create(
                ProcessTypeEnum.TagsExportWords,
                dataSetName, settings,
                string.Format(TagResources.ExportingWordsFrom_0_TagOfDataset_1, tags.Count, dataSetName));

            processHandler.Start(process, (tokenSource) => 
                tagsHandler.ExportWords(process.Id, dataSetName, settings.TagIdList, settings.NGramList, tokenSource.Token, urlProvider.GetHostUrl()));

            return process.ToProcessModel();
        }

        public Result ValidateNGramList(string dataSetName, IEnumerable<int> nGramList)
        {
            if (nGramList == null || !nGramList.Any())
            {
                return Result.Fail(ServiceResources.NGramListCantBeEmpty);
            }
            if (nGramList.Any(n => n > DataSet(dataSetName).DataSet.NGramCount))
            {
                return Result.Fail(ServiceResources.NGramCantBeLargerThanTheNGramCountOfTheDataSet);
            }

            return Result.Ok();
        }
    }
}
