using System.IO;
using System.Text;
using Slamby.Common.Services.Interfaces;

namespace Slamby.Common.Services
{
    public class FileStore : IStore
    {
        string filename { get; set; }

        public FileStore(string filename)
        {
            this.filename = filename;
        }

        public bool Exists() => File.Exists(filename);

        public bool HasContent() => !string.IsNullOrWhiteSpace(Read());

        public string Read()
        {
            if (!Exists())
            {
                return string.Empty;
            }

            return File.ReadAllText(filename);
        }

        public void Write(string content) => File.WriteAllText(filename, content, Encoding.UTF8);
    }
}
