namespace Slamby.Elastic.Models
{
    public class ScrolledSearchResult<T> : SearchResult<T>
    {
        public string ScrollId { get; set; }
    }
}
