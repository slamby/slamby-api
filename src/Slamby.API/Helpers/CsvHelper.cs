using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Slamby.API.Helpers
{
    public static class CsvHelper
    {
        private static string _separator = ";";
        public static void CreateCsv(string filePath, List<List<string>> rows)
        {
            using (Stream stream = File.OpenWrite(filePath))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(true)))
            {
                foreach (var row in rows)
                {
                    var line = string.Join(_separator, row.Select(field => $"\"{field}\""));
                    writer.WriteLine(line);
                    writer.Flush();
                }
            }
            
        }
    }
}
