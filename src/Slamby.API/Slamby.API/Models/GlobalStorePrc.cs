using System.Collections.Generic;
using Slamby.Cerebellum;
using Slamby.Cerebellum.Scorer;
using Slamby.Elastic.Models;

namespace Slamby.API.Models
{
    public class GlobalStorePrc
    {
        public Dictionary<string, PeSScorer> PrcScorers { get; set; } = new Dictionary<string, PeSScorer>();

        public Dictionary<string, Subset> PrcSubsets { get; set; } = new Dictionary<string, Subset>();

        public PrcSettingsElastic PrcsSettings { get; set; } = new PrcSettingsElastic();
    }
}
