using Nest;
using System;
using System.Collections.Generic;

namespace Slamby.Elastic.Models
{
    [ElasticsearchType(Name = "process", IdProperty = "Id")]
    public class ProcessElastic : IModel
    {
        public const string InitObjectMappingName = "init_object";

        [String(Name = "id")]
        public string Id { get; set; }

        [Date(Name = "start")]
        public DateTime Start { get; set; }

        [Date(Name = "end")]
        public DateTime End { get; set; }

        [Number(NumberType.Double, Name = "percent")]
        public double Percent { get; set; }

        [Number(NumberType.Integer, Name = "status")]
        public int Status { get; set; }

        [Number(NumberType.Integer, Name = "type")]
        public int Type { get; set; }

        [Object(Name = InitObjectMappingName)]
        public object InitObject { get; set; }

        [String(Name = "affected_object_id")]
        public object AffectedObjectId { get; set; }

        [String(Name = "error_messages")]
        public List<string> ErrorMessages { get; set; } = new List<string>();

        [String(Name = "description")]
        public string Description { get; set; }

        [String(Name = "result_message")]
        public string ResultMessage { get; set; }

    }
}
