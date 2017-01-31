using System.Collections.Generic;
using Slamby.Cerebellum.Scorer;
using Slamby.Elastic.Models;
using Slamby.SDK.Net.Models;

namespace Slamby.API.Models
{
    public class GlobalStoreClassifier
    {
        public Dictionary<string, PeSScorer> ClassifierScorers { get; set; } = new Dictionary<string, PeSScorer>();

        public Dictionary<string, List<string>> ClassifierEmphasizedTagIds { get; set; } = new Dictionary<string, List<string>>();

        public ClassifierSettingsElastic ClassifiersSettings { get; set; } = new ClassifierSettingsElastic();

        public Dictionary<string, Tag> ClassifierTags { get; set; } = new Dictionary<string, Tag>();

        public Dictionary<string, string> ClassifierParentTagIds { get; set; } = new Dictionary<string, string>();
    }
}
