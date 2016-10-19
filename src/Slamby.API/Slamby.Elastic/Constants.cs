using System.Collections.Generic;

namespace Slamby.Elastic
{
    public static class Constants
    {
        public const string SlambyServicesIndex = "slamby_services";
        public const string SlambyProcessesIndex = "slamby_processes";
        public const string SlambyMetadataIndex = "slamby_metadata";
        public const string SlambyFileParserIndex = "slamby_fileparser";

        public static readonly List<string> ReservedIndices = new List<string>  
        {
            SlambyServicesIndex,
            SlambyProcessesIndex,
            SlambyMetadataIndex,
            SlambyFileParserIndex
        };
    }
}
