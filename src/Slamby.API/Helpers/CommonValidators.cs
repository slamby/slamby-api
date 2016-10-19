using System.Collections.Generic;
using System.Linq;
using Slamby.API.Resources;
using Slamby.Common.Helpers;

namespace Slamby.API.Helpers
{
    public static class CommonValidators
    {
        public static Result ValidateNGramList(IEnumerable<int> list)
        {
            if (list == null || !list.Any())
            {
                return Result.Fail(GlobalResources.NGramListIsEmpty);
            }

            if (list.Any(nGram => nGram < 1 || nGram > 6))
            {
                return Result.Fail(GlobalResources.NGramValuesShouldBeBetween1And6);
            }

            var duplicates = list
                .GroupBy(nGram => nGram)
                .Any(gr => gr.Count() > 1);

            if (duplicates)
            {
                return Result.Fail(GlobalResources.NGramListContainsDuplicates);
            }

            return Result.Ok();
        }

        public static Result ValidateNGramList(IEnumerable<int> list, int dataSetNGramCount)
        {
            return Result.Combine(
                () => ValidateNGramList(list),
                () => ValidateDataSetNGramList(list, dataSetNGramCount));
        }

        public static Result ValidateServiceNGramList(IEnumerable<int> configList, IEnumerable<int> paramlist)
        {
            if (paramlist == null || !paramlist.Any())
            {
                return Result.Fail(GlobalResources.NGramListIsEmpty);
            }

            var commonList = configList.Intersect(paramlist).ToList();
            if (commonList.Count() < paramlist.Count())
            {
                var missingNGrams = paramlist.Except(commonList).ToList();
                return Result.Fail(string.Format(ServiceResources.TheFollowingNGramsNotExistInTheService_0, string.Join(", ", missingNGrams)));
            }

            return Result.Ok();
        }

        private static Result ValidateDataSetNGramList(IEnumerable<int> list, int dataSetNGramCount)
        {
            if (list.Any(nGram => nGram > dataSetNGramCount))
            {
                return Result.Fail(ServiceResources.NGramCantBeLargerThanTheNGramCountOfTheDataSet);
            }

            return Result.Ok();
        }
    }
}
