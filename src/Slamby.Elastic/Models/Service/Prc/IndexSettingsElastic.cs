using System;
using System.Collections.Generic;
using Nest;

namespace Slamby.Elastic.Models
{
    public class IndexSettingsElastic
    {
        [String(Name = "query_filter")]
        public string FilterQuery { get; set; }

        [String(Name = "filter_tagid_list")]
        public List<string> FilterTagIdList { get; set; }

        [Date(Name = "index_date")]
        public DateTime? IndexDate { get; set; }
    }
}
