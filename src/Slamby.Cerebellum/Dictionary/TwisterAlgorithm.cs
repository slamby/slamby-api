using System.Linq;
using Slamby.Cerebellum.Dictionary.Models;
using Slamby.SDK.Net.Models.Enums;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;

namespace Slamby.Cerebellum.Dictionary
{
    /*
     * PMI algorithm
     * https://en.wikipedia.org/wiki/Pointwise_mutual_information
     * https://www.researchgate.net/profile/Huawen_Liu2/publication/221419660_Feature_Selection_Using_Mutual_Information_An_Experimental_Study/links/00b4953c9a42322e85000000.pdf
     *  
     */
     
    public class TwisterAlgorithm : BaseAlgorithm
    {
        readonly bool _chunkPositivePMI;
        readonly bool _chunkAveragePMI;
        readonly Func<int, int, bool> _compiledExpression = null;

        public TwisterAlgorithm(
            Subset subset,
            bool chunkPositivePMI = true,
            bool chunkAveragePMI = true,
            int compressTagOccurence = 0,
            int compressCorpusOccurence = 0,
            LogicalOperatorEnum op = LogicalOperatorEnum.AND,
            bool useRxy = true
            ) : base(subset)
        {

            _chunkPositivePMI = chunkPositivePMI;
            _chunkAveragePMI = chunkAveragePMI;
            if (compressCorpusOccurence > 0 && compressTagOccurence > 0)
            {
                // tOcc <= _compressTagOccurence
                // cOcc <= _compressCorpusOccurence
                var tPe = Expression.Parameter(typeof(int));
                var cPe = Expression.Parameter(typeof(int));

                var tConst = Expression.Constant(compressTagOccurence);
                var cConst = Expression.Constant(compressCorpusOccurence);

                var left = Expression.LessThanOrEqual(tPe, tConst);
                var right = Expression.LessThanOrEqual(cPe, cConst);

                var exp = (op == LogicalOperatorEnum.AND) ? Expression.And(left, right) : Expression.Or(left, right);

                Expression<Func<int, int, bool>> le = Expression.Lambda<Func<int, int, bool>>(exp, tPe, cPe);
                _compiledExpression = le.Compile();
            }
        }

        public override Dictionary<string, double> GetDictionary()
        {
            if (TagDictionary == null) InitTagDictionary();
            var resultDictionary = new Dictionary<string, double>();

            var tdeList = TagDictionary.Values.ToList();

            if (_chunkPositivePMI)
            {
                tdeList.RemoveAll(tde => tde.PMI <= 0);
            }

            var avgPMI = tdeList.Count == 0 ? 0 : tdeList.Select(tde => tde.PMI).Average();
            if (_chunkAveragePMI)
            {
                tdeList.RemoveAll(tde => tde.PMI <= avgPMI);
                avgPMI = tdeList.Count == 0 ? 0 : tdeList.Select(tde => tde.PMI).Average();
            }
            foreach (var tde in tdeList)
            {
                if (_compiledExpression != null && _compiledExpression(tde.OccurenceTag, tde.OccurenceCorpus))
                    continue;
                // Rabxly factor calculation
                var rxf = avgPMI / tde.InformationValueCorpus;
                resultDictionary.Add(tde.Word, tde.PMI / rxf);
            }
            return resultDictionary;
        }
    }
}
