namespace Slamby.Cerebellum.Dictionary.Models
{
    public struct TagDictionaryElement
    {
        public string Word { get; set; }

        /// <summary>
        /// occurence within the tag
        /// </summary>
        public int OccurenceTag { get; set; }

        /// <summary>
        /// occurence within the corpus
        /// </summary>
        public int OccurenceCorpus { get; set; }

        /// <summary>
        /// joint probability (occurences within tag / all words all occurences sum within corpus)
        /// </summary>
        public double JointProb { get; set; }

        /// <summary>
        /// probability within the corpus
        /// </summary>
        public double ProbCorpus { get; set; }

        /// <summary>
        /// information value within the corpus
        /// </summary>
        public double InformationValueCorpus { get; set; }

        /// <summary>
        /// Pointwise Mutual Difference
        /// </summary>
        public double PMI { get; set; }

    }
}
