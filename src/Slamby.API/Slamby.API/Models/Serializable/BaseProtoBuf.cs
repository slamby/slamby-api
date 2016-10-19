using ProtoBuf;
using System;
using System.IO;

namespace Slamby.API.Models.Serializable
{
    public abstract class BaseProtoBuf
    {
        public void Serialize(string path)
        {
            try
            {
                using (var file = File.Create(path))
                {
                    Serializer.Serialize(file, this);
                }
            }
            catch (Exception ex)
            {
                File.Delete(path);
                throw ex;
            }
        }

        public static T DeSerialize<T>(string path)
        {
            using (Stream s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {

                var deserialized = Serializer.Deserialize<T>(s);
                return deserialized;
            }
        }

        public static T DeSerialize<T>(MemoryStream s)
        {
            var deserialized = Serializer.Deserialize<T>(s);
            return deserialized;
        }

        public abstract string GetFileName();
    }
}
