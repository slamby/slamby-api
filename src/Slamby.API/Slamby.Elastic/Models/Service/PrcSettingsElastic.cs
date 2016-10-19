using Nest;
using System;
using System.Collections.Generic;

namespace Slamby.Elastic.Models
{
    [ElasticsearchType(Name = "prc_settings", IdProperty = "ServiceId")]
    public class PrcSettingsElastic : BaseServiceSettingsElastic
    {
        [Object(Name = "tags")]
        public List<TagElastic> Tags { get; set; }

        [String(Name = "fields_for_recommendation", Index = FieldIndexOption.NotAnalyzed)]
        public List<string> FieldsForRecommendation { get; set; }

        [Object(Name = "compress_settings")]
        public CompressSettingsElastic CompressSettings { get; set; }

        [Object(Name = "index_settings")]
        public IndexSettingsElastic IndexSettings { get; set; }
    }
}
