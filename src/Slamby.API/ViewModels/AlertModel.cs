using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.ViewModels
{
    public class AlertModel
    {
        public string ClassName { get; set; } = "alert-info";

        public string Message { get; set; } = string.Empty;
    }
}
