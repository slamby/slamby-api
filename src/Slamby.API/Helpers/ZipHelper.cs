using System.IO.Compression;

namespace Slamby.API.Helpers
{
    public static class ZipHelper
    {
        public static void CompressFolder(string directoryPath, string zipPath)
        {
            ZipFile.CreateFromDirectory(directoryPath, zipPath);
        }
    }
}
