using System;
using System.Collections.Generic;
using Slamby.Cerebellum.Dictionary.Models;

namespace Slamby.Cerebellum.Dictionary
{
    /*
     * PMI algorithm
     * https://en.wikipedia.org/wiki/Pointwise_mutual_information
     * https://www.researchgate.net/profile/Huawen_Liu2/publication/221419660_Feature_Selection_Using_Mutual_Information_An_Experimental_Study/links/00b4953c9a42322e85000000.pdf
     *  
     */

    public abstract class BaseAlgorithm
    {

        protected Subset _subset;
        public Dictionary<string, TagDictionaryElement> TagDictionary;
        public BaseAlgorithm(Subset subset)
        {
            _subset = subset;
        }

        public void InitTagDictionary()
        {
            TagDictionary = new Dictionary<string, TagDictionaryElement>();
            var probTag = (double)_subset.AllWordsOccurencesSumInTag / _subset.AllWordsOccurencesSumInCorpus;

            foreach (var wwo in _subset.WordsWithOccurences)
            {
                var tde = new TagDictionaryElement();
                tde.Word = wwo.Key;
                tde.OccurenceTag = wwo.Value.Tag;
                tde.OccurenceCorpus = wwo.Value.Corpus;
                tde.ProbCorpus = (double)tde.OccurenceCorpus / _subset.AllWordsOccurencesSumInCorpus;
                tde.JointProb = (double)tde.OccurenceTag / _subset.AllWordsOccurencesSumInCorpus;
                tde.InformationValueCorpus = -Math.Log(tde.ProbCorpus, 2);
                tde.PMI = Math.Log(tde.JointProb / (tde.ProbCorpus * probTag), 2);
                TagDictionary.Add(tde.Word, tde);
            }
        }

        public abstract Dictionary<string, double> GetDictionary();
    }
}
