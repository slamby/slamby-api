using Nest;

namespace Slamby.Elastic.Models
{
    [ElasticsearchType(Name = "fileParser")]
    public class FileParserElastic
    {

        public string Content { get; set; }
    }
}
