using System;

namespace Slamby.Common.Config
{
    public class ParallelConfig
    {
        /// <summary>
        /// Value for ParallelOptions.MaxDegreeOfParallelism
        /// 0 means no lower limit from default value
        /// </summary>
        public int ConcurrentTasksLimit { get; set; }
    }
}