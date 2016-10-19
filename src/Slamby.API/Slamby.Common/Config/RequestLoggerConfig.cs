using System.Collections.Generic;

namespace Slamby.Common.Config
{
    public class RequestLoggerConfig
    {
        public List<string> IgnoreContent { get; set; } = new List<string>();
    }
}