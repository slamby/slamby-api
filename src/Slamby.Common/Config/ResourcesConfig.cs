namespace Slamby.Common.Config
{
    public class ResourcesConfig
    {
        public string LogPath { get; set; } = string.Empty;

        public int RefreshInterval { get; set; } = 5000;

        /// <summary>
        /// Request size is maximum this percentage of the FREE memory
        /// </summary>
        public int MaxRequestSizeMemoryPercentage { get; set; }
        /// <summary>
        /// Request size is maximum this, no matter what (in MB)
        /// </summary>
        public int MaxRequestSize { get; set; }

        /// /// <summary>
        /// Max size for indexing to elasticsearch (in number)
        /// </summary>
        public int MaxIndexBulkCount { get; set; }

        /// /// <summary>
        /// Max size for searching to elasticsearch (in number)
        /// </summary>
        public int MaxSearchBulkCount { get; set; }

        /// <summary>
        /// Max size for indexing to elasticsearch (in bytes)
        /// </summary>
        public int MaxIndexBulkSize { get; set; }

    }
}