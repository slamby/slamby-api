using System;
using System.Collections.Generic;
using System.Linq;
using Slamby.Elastic.Models;

namespace Slamby.Elastic.Helpers
{
    public static class TagHelper
    {
        public static Dictionary<int, List<string>> GetTagIdsByLevel<TItem>(IEnumerable<TItem> tags, Func<TItem, string> parentIdSelector, Func<TItem, string> idSelector)
        {
            var tagIdsByLevel = new Dictionary<int, List<string>> { [0] = new List<string> { string.Empty, null, "NULL" } };
            var level = 1;

            do
            {
                var prevLevelIds = tagIdsByLevel[level - 1];
                var tagIds = tags
                    .Where(t => prevLevelIds.Contains(parentIdSelector(t)))
                    .Select(s => idSelector(s))
                    .ToList();

                if (!tagIds.Any())
                {
                    break;
                }

                tagIdsByLevel.Add(level++, tagIds);
            }
            while (level < 100);

            tagIdsByLevel.Remove(0);

            return tagIdsByLevel;
        }

        public static void AdjustTagElastics(List<TagElastic> tags)
        {
            tags.ForEach(t => t.IsLeaf = true);

            foreach (var levelIds in GetTagIdsByLevel(tags, item => item.ParentId(), item => item.Id))
            {
                foreach (var id in levelIds.Value)
                {
                    var tag = tags.FirstOrDefault(t => t.Id == id);
                    if (tag == null)
                    {
                        continue;
                    }

                    tag.Level = levelIds.Key;

                    if (!string.IsNullOrWhiteSpace(tag.ParentId()))
                    {
                        var parentTag = tags.FirstOrDefault(t => t.Id == tag.ParentId());
                        if (parentTag == null)
                        {
                            continue;
                        }

                        parentTag.IsLeaf = false;
                        tag.ParentIdList = new List<string>(parentTag.ParentIdList);
                        tag.ParentIdList.Add(parentTag.Id);
                    }
                }
            }
        }

        public static List<string> GetDescendantTagIds(IEnumerable<TagElastic> tags, string tagId)
        {
            var rootNodes = Node<TagElastic>.CreateTree(tags, t => t.Id, t => t.ParentId());

            var tagNode = rootNodes.SelectMany(n => n.SelfAndDescendants).FirstOrDefault(n => n.Value.Id == tagId);

            if (tagNode != null)
            {
                return tagNode.SelfAndDescendants.Select(t => t.Value.Id).ToList();
            }

            return new List<string>();
        }
    }
}
