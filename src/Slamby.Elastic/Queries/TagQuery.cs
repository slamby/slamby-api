using Nest;
using Slamby.Elastic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Slamby.Common.DI;
using Slamby.Common.Config;

namespace Slamby.Elastic.Queries
{
    [TransientDependency]
    public class TagQuery : BaseQuery
    {
        public TagQuery(ElasticClient client, SiteConfig siteConfig) : base(client, siteConfig)
        {
        }

        public TagElastic Get(string id)
        {
            if (id == null)
            {
                return null;
            }

            var sdesc = new SearchDescriptor<TagElastic>().Query(q => q.Ids(i => i.Values(id)));
            return Get(sdesc).Items.FirstOrDefault();
        }

        public IEnumerable<TagElastic> Get(IEnumerable<string> ids)
        {
            var sdesc = new SearchDescriptor<TagElastic>().Query(q => q.Ids(i => i.Values(ids)));
            return Get(sdesc).Items;
        }

        public SearchResult<TagElastic> GetAll()
        {
            var sdesc = new SearchDescriptor<TagElastic>();
            return Get(sdesc);
        }

        public string Index(TagElastic tagElastic)
        {
            return IndexWithBulkResponse(new List<TagElastic> { tagElastic }).Items.FirstOrDefault().Id;
        }

        public IBulkResponse IndexWithBulkResponse(IEnumerable<TagElastic> tagElastics)
        {
            if (!tagElastics.Any())
            {
                return null;
            }

            var response = Client.IndexMany(tagElastics);
            ResponseValidator(response);
            ResponseValidator(Client.Flush(IndexName));
            return response;
        }

        public NestBulkResponse ParallelBulkIndex(IEnumerable<TagElastic> tagElastics, int parallelLimit, decimal objectsSizeInBytes)
        {
            var response = base.ParallelBulkIndex(tagElastics, parallelLimit, objectsSizeInBytes);
            return response;
        }

        public NestBulkResponse ParallelBulkIndex(IEnumerable<TagElastic> tagElastics, int parallelLimit)
        {
            var response = base.ParallelBulkIndex(tagElastics, parallelLimit);
            return response;
        }

        public string Update(string id, TagElastic tagElastic)
        {
            Delete(id);
            Index(tagElastic);
            ResponseValidator(Client.Flush(IndexName));
            return tagElastic.Id;
        }

        public void DeleteAll()
        {
            var ids = GetAll().Items.Select(i => i.Id).ToList();
            if (!ids.Any())
            {
                return;
            }

            var response = Client.DeleteMany(ids.Select(id => new TagElastic { Id = id }));
            ResponseValidator(response);
            ResponseValidator(Client.Flush(IndexName));
        }

        public bool Delete(string id)
        {
            var deleteResponse = Client.Delete<TagElastic>(id);
            ResponseValidator(deleteResponse);
            ResponseValidator(Client.Flush(IndexName));
            return true;
        }
        
        public bool IsExists(string id)
        {
            return Client.DocumentExists<TagElastic>(id).Exists;
        }

        /// <summary>
        /// Returns sibling id list of the given tag
        /// </summary>
        /// <param name="tagId"></param>
        /// <returns></returns>
        public List<string> GetSiblingTagIds(string tagId)
        {
            var result = new List<string>();
            var tag = Get(tagId);
            if (tag == null || !tag.ParentIdList.Any())
            {
                return result;
            }

            var parentId = tag.ParentIdList.First();
            var searchDescriptor = new SearchDescriptor<TagElastic>().Query(q => q.Term(t => t.ParentIdList, parentId));
            var response = Get(searchDescriptor);

            result = response.Items
                .Select(d => d.Id)
                .Except(new List<string> { tag.Id })
                .ToList();

            return result;
        }

        public List<string> GetDescendantTagIds(string tagId)
        {
            return GetDescendantTags(tagId).Select(s => s.Id).ToList();
        }

        public List<TagElastic> GetDescendantTags(string tagId)
        {
            var result = new List<TagElastic>();
            var tag = Get(tagId);
            if (tag == null)
            {
                return result;
            }

            var response = Get(GetDescendantSearchDescriptor(tagId));

            result = response.Items.ToList();

            return result;
        }

        public bool HasChildren(TagElastic tagElastic)
        {
            if (tagElastic == null)
            {
                throw new ArgumentNullException(nameof(tagElastic));
            }

            var response = Count<TagElastic>(GetDescendantSearchDescriptor(tagElastic.Id));

            return response > 0;
        }

        private static SearchDescriptor<TagElastic> GetDescendantSearchDescriptor(string tagId)
        {
            return new SearchDescriptor<TagElastic>().Query(q => q.Term(t => t.ParentIdList, tagId));
        }

        public void AdjustLeafStatus(TagElastic tagElastic)
        {
            if (tagElastic == null)
            {
                return;
            }

            var hasChildren = GetDescendantTagIds(tagElastic.Id).Any();
            if (hasChildren && tagElastic.IsLeaf == true)
            {
                tagElastic.IsLeaf = false;
                Update(tagElastic.Id, tagElastic);
            }
            else if (!hasChildren && tagElastic.IsLeaf == false)
            {
                tagElastic.IsLeaf = true;
                Update(tagElastic.Id, tagElastic);
            }
        }

        public void DeleteByIds(IEnumerable<string> ids)
        {
            var response = Client.DeleteMany(ids.Select(id => new TagElastic { Id = id }));
            ResponseValidator(response);
            ResponseValidator(Client.Flush(IndexName));
        }
    }
}
