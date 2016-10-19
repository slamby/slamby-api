using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Slamby.Cerebellum.Helpers
{
    public static class NGramMaker
    {
        public static List<string> GetNgrams(string text, int nGramSize, bool getShorterGrmas = false)
        {
            if (nGramSize == 0) throw new Exception("nGram size was not set");
            if (string.IsNullOrEmpty(text)) return new List<string>();
            return nGramSize == 1 ? _getUniGramAbsolute(text).ToList() : _getNgramsAbsolute(text, nGramSize, getShorterGrmas).ToList();
        }

        private static IEnumerable<string> _getUniGramAbsolute(string text)
        {
            return text.Split(new []{' '}, StringSplitOptions.RemoveEmptyEntries);
        }

        private static IEnumerable<string> _getNgramsAbsolute(string text, int nGramSize, bool getShorterGrmas)
        {
            var nGramList = new List<string>();
            var nGram = new StringBuilder();
            var wordLengths = new Queue<int>();

            var wordCount = 0;
            var lastWordLen = 0;

            //append the first character, if valid.
            //avoids if statement for each for loop to check i==0 for before and after vars.
            if (text != "")
            {
                nGram.Append(text[0]);
                lastWordLen++;
            }

            //generate ngrams
            for (var i = 1; i < text.Length - 1; i++)
            {
                if (text[i] != ' ')
                {
                    nGram.Append(text[i]);
                    lastWordLen++;
                }
                else
                {
                    if (lastWordLen > 0)
                    {
                        wordLengths.Enqueue(lastWordLen);
                        lastWordLen = 0;
                        wordCount++;

                        if (wordCount >= nGramSize)
                        {
                            nGramList.Add(nGram.ToString());
                            nGram.Remove(0, wordLengths.Dequeue() + 1);
                            wordCount--;
                        }
                        nGram.Append(' ');
                    }
                }
            }
            nGram.Append(text[text.Length - 1]);
            if (wordCount + 1 == nGramSize || getShorterGrmas) nGramList.Add(nGram.ToString());

            return nGramList;
        }
    }
}
