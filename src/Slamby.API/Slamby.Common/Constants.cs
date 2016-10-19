namespace Slamby.Common
{
    public static class Constants
    {
        public const string FilesPath = "/files";

        /// <summary>
        /// Current API Elasticsearch DB version (DataSet)
        /// PropertiesElastic stores its actual value
        /// </summary>
        public const int DBVersion = 2;

        /// <summary>
        /// Global DB Version used in Metadata
        /// </summary>
        public const int MetadataDBVersion = 3;

        /// <summary>
        /// how many times the dictionaries larger in memory than on disk
        /// </summary>
        public const int DictionaryInMemoryMultiplier = 3;

        /// <summary>
        /// what is the separator between the texts in the DocumentElastic Text field
        /// </summary>
        public const string TextFieldSeparator = "7bm4l5";
    }
}
