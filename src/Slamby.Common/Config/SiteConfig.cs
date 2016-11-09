namespace Slamby.Common.Config
{
    public class SiteConfig
    {
        public string BaseUrlPrefix { get; set; }

        public string Version { get; set; } = "0.0.0.0";

        public ParallelConfig Parallel { get; set; } = new ParallelConfig();

        public DirectoryConfig Directory { get; set; } = new DirectoryConfig();

        public ElasticSearchConfig ElasticSearch { get; set; } = new ElasticSearchConfig();

        public SerilogConfig Serilog { get; set; } = new SerilogConfig();

        public RedisConfig Redis { get; set; } = new RedisConfig();

        public ElmConfig Elm { get; set; } = new ElmConfig();

        public ResourcesConfig Resources { get; set; } = new ResourcesConfig();

        public RequestLoggerConfig RequestLogger { get; set; } = new RequestLoggerConfig();
    }
}
