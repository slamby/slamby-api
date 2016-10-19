using System.Collections.Generic;

namespace Slamby.Elastic.Models
{
    public class NestBulkResponse
    {
        public List<Nest.BulkResponseItemBase> Items { get; set; } = new List<Nest.BulkResponseItemBase>();
        public List<Nest.BulkResponseItemBase> ItemsWithErrors { get; set; } = new List<Nest.BulkResponseItemBase>();
    }
}
