using Nest;
using System;
using System.Collections.Generic;
using Slamby.SDK.Net.Models.Enums;

namespace Slamby.Elastic.Models
{
    [ElasticsearchType(Name = "service", IdProperty = "Id")]
    public class ServiceElastic : IModel
    {
        [String(Name = "id")]
        public string Id { get; set; }

        [String(Name = "service_name")]
        public string Name { get; set; }

        [String(Name = "service_alias", Index = FieldIndexOption.NotAnalyzed)]
        public string Alias { get; set; }

        [Number(NumberType.Integer, Name = "status")]
        public int Status { get; set; }

        [String(Name = "service_description")]
        public string Description { get; set; }

        [String(Name = "type")]
        public int Type { get; set; }

        [String(Name = "process_id_list")]
        public List<string> ProcessIdList { get; set; } = new List<string>();

        public static ServiceElastic Create(string name, ServiceTypeEnum type, string alias = "", string description = "")
        {
            return new ServiceElastic
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Alias = alias,
                Description = description,
                Type = (int)type,
                Status = (int)ServiceStatusEnum.New
            };
        }
    }
}
