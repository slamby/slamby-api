using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.SDK.Net.Models;

namespace Slamby.Common.Services
{
    [SingletonDependency]
    public class MachineResourceService
    {
        readonly ILogger logger;
        readonly bool IsWindowsOS = Environment.GetEnvironmentVariable("OS") == "Windows_NT";
        readonly string path;

        public CancellationToken StopToken { get; }
        public Status Status { get; }
        public decimal MaxRequestSize { get; private set; }

        private object _statusLock = new object();
        
        readonly SiteConfig siteConfig;

        public MachineResourceService(ILogger<MachineResourceService> logger, SiteConfig siteConfig, IApplicationEnvironment env,
            IApplicationLifetime applicationLifetime)
        {
            this.siteConfig = siteConfig;
            this.logger = logger;

            path = siteConfig.Resources.LogPath;
            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(Path.Combine(env.ApplicationBasePath, path));
            }

            logger.LogInformation($"{nameof(MachineResourceService)} using resource path: {path}");

            Status = new Status
            {
                ApiVersion = siteConfig.Version,
                ProcessorCount = Environment.ProcessorCount
            };

            StopToken = applicationLifetime.ApplicationStopping;
        }

        public void StartBackgroundUpdater()
        {
            new TaskFactory().StartNew(GetResources, TaskCreationOptions.LongRunning);
        }

        public void UpdateResourcesManually()
        {
            RefreshResource();
        }

        private void GetResources()
        {
            while (!StopToken.IsCancellationRequested)
            {
                RefreshResource();
                try
                {
                    Task.Delay(siteConfig.Resources.RefreshInterval)
                        .Wait(StopToken);
                }
                catch (OperationCanceledException)
                {
                    // If token cancelled OperationCanceledException is thrown, but it is expected exception
                    break;
                }
            }

            logger.LogInformation($"{nameof(MachineResourceService)} stopped");
        }

        private void RefreshResource()
        {
            try
            {
                var freeSpace = 0L;

                if (IsWindowsOS)
                {
                    freeSpace = DriveInfo.GetDrives()
                                        .Where(drive => drive.DriveType == DriveType.Fixed)
                                        .Select(drive => drive.AvailableFreeSpace)
                                        .Sum();
                }
                else
                {
                    freeSpace = DriveInfo.GetDrives()
                                        .Where(drive => drive.Name == "/")
                                        .Select(drive => drive.AvailableFreeSpace)
                                        .FirstOrDefault();
                }
                
                var cpu = ReadFileContent("cpu.log");
                var mem = ReadFileContent("mem.log");

                lock (_statusLock)
                {
                    Status.AvailableFreeSpace = ((decimal)freeSpace / (1024 * 1024)).Round(2);
                    Status.CpuUsage = cpu.ToDecimal().Round(2);

                    var freeLines = mem.Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                    if (freeLines.Length >= 2)
                    {
                        Status.FreeMemory = freeLines[0].ToDecimal(1024).Round(2);
                        Status.TotalMemory = freeLines[1].ToDecimal(1024).Round(2);

                        //set max request size to the percentage of the memory
                        MaxRequestSize = (Status.FreeMemory * siteConfig.Resources.MaxRequestSizeMemoryPercentage / 100).Round(0);
                        //if the configuration's limit is larger than this than set the limit 
                        if (MaxRequestSize == 0 || MaxRequestSize > siteConfig.Resources.MaxRequestSize) MaxRequestSize = siteConfig.Resources.MaxRequestSize;
                        MaxRequestSize = MaxRequestSize * 1024 * 1024;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"{nameof(GetResources)} error", ex);
            }
        }

        private string ReadFileContent(string filename)
        {
            var value = string.Empty;
            var resourceFilename = string.Empty;

            try
            {
                resourceFilename = Path.Combine(path, filename);
                using (var stream = File.Open(resourceFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(stream, true))
                    {
                        value = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Could not read resource file {resourceFilename}", ex);
            }

            return value;
        }
    }

    public static class ResourceConvertHelper
    {
        public static decimal ToDecimal(this string text, decimal divisor = 1)
        {
            decimal value = 0;
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            {
                return 0;
            }

            return value / divisor;
        }

        public static decimal Round(this decimal number, int decimals)
        {
            return Math.Round(number, decimals);
        }

        public static double Round(this double number, int decimals)
        {
            return Math.Round(number, decimals);
        }
    }
}
