using Slamby.Elastic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Models
{
    public class GlobalStoreSearch
    {
        public SearchSettingsElastic SearchSettings { get; set; } = new SearchSettingsElastic();

        public HighlightSettingsElastic HighlightSettings { get; set; } = new HighlightSettingsElastic();

        public AutoCompleteSettingsElastic AutoCompleteSettings { get; set; } = new AutoCompleteSettingsElastic();

        public ClassifierSearchSettingsElastic ClassifierSettings { get; set; } = new ClassifierSearchSettingsElastic();
    }
}
