using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Slamby.API.Helpers
{
    //HACK: https://github.com/dotnet/corefx/issues/8768
    //this should be removed when https://github.com/dotnet/corefx/issues/11564 is closed
    public static class RedisDnsHelper
    {
        public static ConfigurationOptions CorrectOption(ConfigurationOptions options)
        {
            try
            {
                //HACK: https://github.com/dotnet/corefx/issues/8768
                //this should be removed when https://github.com/dotnet/corefx/issues/11564 is closed
                var dnsEndPoints = options.EndPoints.OfType<DnsEndPoint>().ToList();
                foreach (var dnsEndPoint in dnsEndPoints)
                {
                    options.EndPoints.Remove(dnsEndPoint);
                    options.EndPoints.Add(GetIp(dnsEndPoint.Host).Result, dnsEndPoint.Port);
                }
                if (options.WriteBuffer < 64 * 1024)
                {
                    options.WriteBuffer = 64 * 1024;
                }
                options.ResolveDns = false; //re-resolve dns on re-connect
                return options;
            } catch(Exception ex)
            {
                return null;
            }
            
        }

        private static async Task<string> GetIp(string hostname)
             => (await Dns.GetHostEntryAsync(hostname)).AddressList.First(a => a.AddressFamily == AddressFamily.InterNetwork).ToString();

    }
}
