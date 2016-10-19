using System.Collections.Generic;
using System.Linq;

namespace Slamby.Cerebellum.Scorer
{
    public abstract class BaseScorer
    {
        protected Dictionary<int, Dictionary<string, double>> _dictionaries;
        protected Dictionary<int, double> _maxValues;
        public BaseScorer(Dictionary<int, Dictionary<string, double>> dictionaries)
        {
            _dictionaries = dictionaries;
            _maxValues = dictionaries.ToDictionary(d => d.Key, d => (d.Value == null || !d.Value.Any()) ? 0.0 : d.Value.Values.Max());
        }
        public abstract double GetScore(string text, double nGramMultiplier, bool normalized);

        
    }
}
