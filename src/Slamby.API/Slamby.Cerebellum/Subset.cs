using Slamby.Elastic.Models;
using System.Collections.Generic;

namespace Slamby.Cerebellum
{
    public class Subset
    {
        /// <summary>
        /// words with their occurences within corpus and within tag
        /// </summary>
        public Dictionary<string, Occurences> WordsWithOccurences { get; set; }

        /// <summary>
        /// all words all occurences sum within corpus
        /// </summary>
        public int AllWordsOccurencesSumInCorpus { get; set; }

        /// <summary>
        /// all words all occurences sum within tag
        /// </summary>
        public int AllWordsOccurencesSumInTag { get; set; }
        
    }
}
