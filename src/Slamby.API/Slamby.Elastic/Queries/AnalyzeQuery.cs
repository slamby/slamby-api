using Nest;
using System.Collections.Generic;
using System.Linq;
using Slamby.Common.DI;
using Slamby.Common.Config;

namespace Slamby.Elastic.Queries
{
    [TransientDependency]
    public class AnalyzeQuery : BaseQuery
    {
        public AnalyzeQuery(ElasticClient client, SiteConfig siteConfig) : base(client, siteConfig) { }

        public IEnumerable<string> Analyze(string text, int nGramCount)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            var request = new AnalyzeRequest(IndexName, text)
            {
                Analyzer = $"{_analyzerPrefix}{nGramCount}"
                
            };
            var result = Client.Analyze(request);
            ResponseValidator(result);
            return result.Tokens.Select(t => t.Token);
        }
    }
}
