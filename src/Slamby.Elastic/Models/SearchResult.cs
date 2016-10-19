using System.Collections.Generic;

namespace Slamby.Elastic.Models
{
    public class SearchResult<T>
    {
        public IEnumerable<T> Items { get; set; }

        public long Total { get; set; }
    }
}
