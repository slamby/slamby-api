using System.Threading;
using System.Threading.Tasks;
using Slamby.Elastic.Models;

namespace Slamby.API.Models
{
    public class GlobalStoreProcess
    {
        public ProcessElastic Process { get; set; }
        public CancellationTokenSource CancellationToken { get; set; }
        public Task Task { get; set; }
    }
}