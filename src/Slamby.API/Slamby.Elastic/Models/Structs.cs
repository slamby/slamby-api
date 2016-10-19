namespace Slamby.Elastic.Models
{
    /// <summary>
    /// szóelőfordulások
    /// </summary>
    [ProtoBuf.ProtoContract]
    public struct Occurences
    {
        /// <summary>
        /// occurence within the tag
        /// </summary>
        [ProtoBuf.ProtoMember(1)]
        public int Tag { get; set; }
        /// <summary>
        /// occurence within the corpus
        /// </summary>
        [ProtoBuf.ProtoMember(2)]
        public int Corpus { get; set; }
    }
}