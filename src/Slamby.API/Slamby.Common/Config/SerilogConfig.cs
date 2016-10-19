namespace Slamby.Common.Config
{
    public class SerilogConfig
    {
        public string Output { get; set; }

        public int RetainedFileCountLimit { get; set; }

        public string MinimumLevel { get; set; } = "Debug";
    }
}
