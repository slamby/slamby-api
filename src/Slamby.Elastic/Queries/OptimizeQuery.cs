using Nest;
using Slamby.Common.Config;
using Slamby.Common.DI;

namespace Slamby.Elastic.Queries
{
    [TransientDependency]
    public class OptimizeQuery : BaseQuery
    {
        public OptimizeQuery(ElasticClient client, SiteConfig siteConfig) : base(client, siteConfig) { }

        /// <summary>
        /// Elasticsearch optimize
        /// </summary>
        /// <param name="onlyExpungeDeletes">should the merge process only expunge segments with deletes in it</param>
        /// <param name="maxSegmentNumber">the number of segments to merge to. 0 -> simply checking if a merge needs to execute, and if so, executes it</param>
        public void Optimize(bool onlyExpungeDeletes = false, int maxSegmentNumber = 0)
        {
            var clusterPutResponse = Client.ClusterPutSettings(cs => cs.Transient(p => p.Add("indices.store.throttle.type", "none")));
            ResponseValidator(clusterPutResponse);
            try
            {
                var optimizeDesc = new OptimizeDescriptor();
                optimizeDesc = optimizeDesc.OnlyExpungeDeletes(onlyExpungeDeletes);
                if (maxSegmentNumber > 0) optimizeDesc = optimizeDesc.MaxNumSegments(maxSegmentNumber);
                var response = Client.Optimize(IndexName, o => optimizeDesc);
                ResponseValidator(response);
            }
            finally
            {
                clusterPutResponse = Client.ClusterPutSettings(cs => cs.Transient(p => p.Add("indices.store.throttle.type", "merge")));
                ResponseValidator(clusterPutResponse);
            }
        }

    }
}
