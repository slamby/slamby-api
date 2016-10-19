using Nest;

namespace Slamby.Elastic.Models
{
    [ElasticsearchType(Name = "metadata")]
    public class MetadataElastic
    {
        [Number(NumberType.Integer, Name = "db_version")]
        public int DBVersion { get; set; }
    }
}
