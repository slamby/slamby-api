using System;
using System.Collections.Generic;
using System.Linq;

namespace Slamby.Cerebellum.Scorer
{
    public class PeSScorer : BaseScorer
    {
        public PeSScorer(Dictionary<int, Dictionary<string, double>> dictionaries):base(dictionaries) {}

        public override double GetScore(string text, double nGramMultiplier, bool normalized = true)
        {
            if (string.IsNullOrEmpty(text)) return 0.0;
            var keys = _dictionaries.Keys.OrderByDescending(k => k).ToList();
            var maxN = _dictionaries.Keys.Max();
            List<string> words;
            do
            {
                words = Helpers.NGramMaker.GetNgrams(text, maxN).ToList();
                if (words.Count == 0) {
                    keys.Remove(maxN--);
                }
            } while (words.Count == 0 && maxN > 0);

            if (_dictionaries.All(d => d.Value == null)) return -1;

            var normalizedDenominator = 1.0;
            if (normalized)
            {
                foreach (var t in _dictionaries)
                {
                    var actualDeno = 1.0;
                    actualDeno = actualDeno * _maxValues[t.Key] * Math.Pow(nGramMultiplier, t.Key - 1) * Math.Pow(2, _dictionaries.Keys.Max() - t.Key);
                    if (actualDeno > normalizedDenominator) normalizedDenominator = actualDeno;
                }
            }

            var score = _getScore(words, keys, nGramMultiplier);
            score = (score / words.Count) / normalizedDenominator;
            return score;
        }


        private double _getScore(List<string> words, List<int> keys, double nGramMultiplier)
        {
            var actualNGram = keys.First();
            var td = _dictionaries[actualNGram];
            var score = 0.0;
            if (td != null && td.Any())
            {
                var foundWords = words.Where(w => td.ContainsKey(w)).ToList();

                //weighting the n-grams
                var blockFoundWordsWithValue = foundWords.Select(bfw =>
                    new KeyValuePair<string, double>(
                        bfw,
                        td[bfw] * Math.Pow(nGramMultiplier, actualNGram - 1))).ToList();

                score = blockFoundWordsWithValue.Sum(bfwv => bfwv.Value);

                var missings = foundWords.Aggregate(words.ToList(), (l, e) => { l.Remove(e); return l; }).ToList(); // setB - setA
                if (keys.Count > 1)
                {
                    words = new List<string>();
                    missings.ForEach(w => words.AddRange(Helpers.NGramMaker.GetNgrams(w, actualNGram - 1)));
                    keys.Remove(actualNGram);

                    var gotScore = _getScore(words, keys, nGramMultiplier);
                    score += gotScore;
                }
            }
            return score;
        }
    }
}
