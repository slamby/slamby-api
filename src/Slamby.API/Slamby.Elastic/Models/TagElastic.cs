using Nest;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Slamby.Elastic.Models
{
    [ElasticsearchType(Name = "tag", IdProperty ="Id")]
    public class TagElastic : IModel
    {
        public string Id { get; set; }
        [String(Name = "name", Index = FieldIndexOption.NotAnalyzed)]
        public string Name { get; set; }

        [Date(Name = "created_date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Date(Name = "modified_date")]
        public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

        [String(Name = "parentIdList", Index = FieldIndexOption.NotAnalyzed)]
        public List<string> ParentIdList { get; set; } = new List<string>();

        [Boolean(Name = "is_leaf")]
        public bool IsLeaf { get; set; }

        [Number(NumberType.Integer, Name = "level")]
        public int Level { get; set; }

        public string ParentId ()
        {
            if (ParentIdList == null || !ParentIdList.Any())
            {
                return null;
            }

            return ParentIdList.Last();
        }
    }
}
