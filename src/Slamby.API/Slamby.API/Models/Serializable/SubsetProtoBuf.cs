using ProtoBuf;
using System.Collections.Generic;

namespace Slamby.API.Models.Serializable
{
    [ProtoContract]
    public class SubsetProtoBuf : BaseProtoBuf
    {
        [ProtoMember(1)]
        public string Id { get; set; }
        [ProtoMember(2)]
        public int AllOccurencesSumInCorpus { get; set; }
        [ProtoMember(3)]
        public Dictionary<string, Elastic.Models.Occurences> WordsWithOccurences { get; set; }
        [ProtoMember(4)]
        public int AllWordsOccurencesSumInTag { get; set; }
        
        public override string GetFileName()
        {
            var fileName = string.Format("{0}.{1}", Id, GetExtension());
            return fileName;
        }

        public static string GetExtension()
        {
            return "slang";
        }
    }
}
