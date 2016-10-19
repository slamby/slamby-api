using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Slamby.API.Helpers
{
    public static class IOHelper
    {
        public static List<string> GetFilesInFolder(string folder, string extension = null)
        {
            List<string> files;
            if (extension == null) files = Directory.GetFiles(folder).ToList();
            else files = Directory.GetFiles(folder, string.Format("*.{0}", extension)).ToList();
            foreach (var directory in Directory.GetDirectories(folder))
            {
                files.AddRange(GetFilesInFolder(directory, extension));
            }
            return files;
        }

        public static void SafeDeleteDictionary(string path, bool recursive = false)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive);
            }
        }
    }
}
