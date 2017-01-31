using Slamby.Elastic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Models
{
    public class GlobalStoreSearch
    {
        public SearchSettingsWrapperElastic SearchSettingsWrapper { get; set; } = new SearchSettingsWrapperElastic();
    }
}
