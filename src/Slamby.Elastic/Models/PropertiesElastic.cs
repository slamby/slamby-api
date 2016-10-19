using Nest;
using System;
using System.Collections.Generic;

namespace Slamby.Elastic.Models
{
    [ElasticsearchType(Name = "properties")]
    public class PropertiesElastic
    {
        [String(Name = "name", Index = FieldIndexOption.NotAnalyzed)]
        public string Name { get; set; }

        [Number(NumberType.Integer, Name = "ngram_count")]
        public int NGramCount { get; set; }

        [String(Name = "id_field", Index = FieldIndexOption.NotAnalyzed)]
        public string IdField { get; set; }

        [String(Name = "interpreted_fields", Index = FieldIndexOption.NotAnalyzed)]
        public List<string> InterPretedFields { get; set; }

        [String(Name = "tag_field", Index = FieldIndexOption.NotAnalyzed)]
        public string TagField { get; set; }

        [Obsolete("Use MetadataElastic.DBVersion for database version information")]
        [Number(NumberType.Integer, Name = "db_version")]
        public int DBVersion { get; set; }

        [Object(Name = "sample_document")]
        public object SampleDocument { get; set; }

        [Object(Name = "schema")]
        public object Schema { get; set; }
    }
}
