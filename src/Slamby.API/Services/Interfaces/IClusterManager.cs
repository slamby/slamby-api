using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Slamby.API.Services
{
    public interface IClusterManager
    {
        void StartBackgroundMembersCheck();
    }
}
