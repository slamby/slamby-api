namespace Slamby.Common.Config
{
    public class RedisConfig
    {
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Should contain ...,abortConnect=false,...
        /// </summary>
        public string Configuration { get; set; }
    }
}