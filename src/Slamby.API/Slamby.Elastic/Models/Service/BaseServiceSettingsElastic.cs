using Nest;
using System;

namespace Slamby.Elastic.Models
{
    public abstract class BaseServiceSettingsElastic
    {
        [String(Name = "service_id")]
        public string ServiceId { get; set; }

        [String(Name = "dataset_name", Index = FieldIndexOption.NotAnalyzed)]
        public string DataSetName { get; set; }
    }
}
