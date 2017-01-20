using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.Elastic.Models
{
    [ElasticsearchType(Name = "search_settings", IdProperty = "ServiceId")]
    public class SearchSettingsWrapperElastic : BaseServiceSettingsElastic
    {
        public SearchSettingsWrapperElastic() {}

        public SearchSettingsWrapperElastic(SearchSettingsWrapperElastic value)
        {
            this.AutoCompleteSettings = value.AutoCompleteSettings;
            this.ClassifierSettings = value.ClassifierSettings;
            this.Count = value.Count;
            this.DataSetName = value.DataSetName;
            this.SearchSettings = value.SearchSettings;
            this.ServiceId = value.ServiceId;
        }

        [Number(NumberType.Integer, Name = "count")]
        public int Count { get; set; }

        [Object(Name = "autocomplete_settings")]
        public AutoCompleteSettingsElastic AutoCompleteSettings { get; set; }

        [Object(Name = "search_settings")]
        public SearchSettingsElastic SearchSettings { get; set; }

        [Object(Name = "classifier_settings")]
        public ClassifierSearchSettingsElastic ClassifierSettings { get; set; }

    }

    public class AutoCompleteSettingsElastic
    {
        [Number(NumberType.Double, Name = "confidence")]
        public double Confidence { get; set; }

        [Number(NumberType.Double, Name = "maximum_errors")]
        public double MaximumErrors { get; set; }

        [Number(NumberType.Integer, Name = "count")]
        public int Count { get; set; }
    }
    
    public class SearchSettingsElastic
    {
        [Object(Name = "filter")]
        public FilterElastic Filter { get; set; }

        [Object(Name = "weights")]
        public List<WeightElastic> Weights { get; set; }

        [String(Name = "response_field_list")]
        public List<string> ResponseFieldList { get; set; }

        [String(Name = "search_field_list")]
        public List<string> SearchFieldList { get; set; }

        [Number(NumberType.Integer, Name = "type")]
        public int Type { get; set; }

        [Number(NumberType.Double, Name = "cut_off_frequency")]
        public double CutOffFrequency { get; set; }

        [Number(NumberType.Integer, Name = "fuzziness")]
        public int Fuzziness { get; set; }
        
        [Number(NumberType.Integer, Name = "count")]
        public int Count { get; set; }

        [Number(NumberType.Integer, Name = "operator")]
        public int Operator { get; set; }
    }

    public class ClassifierSearchSettingsElastic
    {
        [String(Name = "id")]
        public string Id { get; set; }

        [Number(NumberType.Integer, Name = "count")]
        public int Count { get; set; }
    }


    public class FilterElastic
    {
        [String(Name = "query")]
        public string Query { get; set; }

        [String(Name = "tagid_list")]
        public List<string> TagIdList { get; set; }
    }

    public class WeightElastic
    {
        [String(Name = "query")]
        public string Query { get; set; }
        [Number(NumberType.Double, Name = "value")]
        public double Value { get; set; }
    }
}
