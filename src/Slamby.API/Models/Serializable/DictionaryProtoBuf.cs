using ProtoBuf;
using System.Collections.Generic;

namespace Slamby.API.Models.Serializable
{
    //note: protobuf-net not handle explicit default values if a property is missing
    [ProtoContract]
    public class DictionaryProtoBuf : BaseProtoBuf
    {
        [ProtoMember(1)]
        public static readonly int Version = 1;

        [ProtoMember(2)]
        public string Id { get; set; }

        [ProtoMember(3)]
        public Dictionary<string, double> Dictionary { get; set; }

        [ProtoMember(4)]
        public int  NGram { get; set; }


        public override string GetFileName()
        {
            return GetFileName(Id);
        }

        public static string GetFileName(string id)
        {
            var fileName = string.Format("{0}.{1}", id, GetExtension());
            return fileName;
        }

        public static string GetExtension()
        {
            return "sdic";
        }
    }
}
