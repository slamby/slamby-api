using Nest;
using Slamby.Elastic.Models;
using System;
using System.Collections.Generic;

namespace Slamby.Elastic.Models
{
    [ElasticsearchType(Name = "classifier_settings", IdProperty = "ServiceId")]
    public class ClassifierSettingsElastic : BaseServiceSettingsElastic
    {
        [Object(Name = "tags")]
        public List<TagElastic> Tags { get; set; }
        [Number(NumberType.Integer, Name = "ngram_list")]
        public List<int> NGramList { get; set; } = new List<int>();

        [String(Name = "activated_tagid_list")]
        public List<string> ActivatedTagIdList { get; set; }
        [String(Name = "emphasized_tagid_list")]
        public List<string> EmphasizedTagIdList { get; set; }
        [Number(NumberType.Integer, Name = "activated_ngram_list")]
        public List<int> ActivatedNGramList { get; set; } = new List<int>();

        [Object(Name = "compress_settings")]
        public CompressSettingsElastic CompressSettings { get; set; }
    
    }
    
}
