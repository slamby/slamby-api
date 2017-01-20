using Slamby.Elastic.Models;
using Slamby.SDK.Net.Models;
using Slamby.SDK.Net.Models.Enums;
using Slamby.SDK.Net.Models.Services;
using System.Linq;
using static Slamby.SDK.Net.Models.Services.SearchSettings;

namespace Slamby.API.Helpers
{
    public static class ModelHelper
    {
        public static T ToServiceModel<T>(this ServiceElastic service) where T : Service, new()
        {
            return new T
            {
                Id = service.Id,
                Name = service.Name,
                Alias = service.Alias,
                Description = service.Description,
                Type = (ServiceTypeEnum)service.Type,
                Status = (ServiceStatusEnum)service.Status,
                ProcessIdList = service.ProcessIdList,
                ActualProcessId = null
            };
        }

        public static Process ToProcessModel(this ProcessElastic process)
        {
            var model = new Process
            {
                Id = process.Id,
                Start = process.Start,
                End = process.End,
                Percent = process.Percent,
                Description = process.Description,
                Status = (ProcessStatusEnum)process.Status,
                ErrorMessages = process.ErrorMessages,
                ResultMessage = process.ResultMessage,
                Type = (ProcessTypeEnum)process.Type
            };

            return model;
        }

        #region SearchService
        public static Filter ToFilterModel(this FilterElastic filter)
        {
            var model = new Filter
            {
                Query = filter.Query,
                TagIdList = filter.TagIdList
            };
            return model;
        }

        public static FilterElastic ToFilterElastic(this Filter filter)
        {
            var model = new FilterElastic
            {
                Query = filter.Query,
                TagIdList = filter.TagIdList
            };
            return model;
        }

        public static Weight ToWeightModel(this WeightElastic weight)
        {
            var model = new Weight
            {
                Query = weight.Query,
                Value = weight.Value
            };
            return model;
        }

        public static WeightElastic ToWeightElastic(this Weight weight)
        {
            var model = new WeightElastic
            {
                Query = weight.Query,
                Value = weight.Value
            };
            return model;
        }

        public static AutoCompleteSettings ToAutoCompleteSettingsModel(this AutoCompleteSettingsElastic autoComplete)
        {
            var model = new AutoCompleteSettings
            {
                Confidence = autoComplete.Confidence,
                Count = autoComplete.Count,
                MaximumErrors = autoComplete.MaximumErrors,
            };
            return model;
        }

        public static AutoCompleteSettingsElastic ToAutoCompleteSettingsElastic(this AutoCompleteSettings autoComplete)
        {
            var model = new AutoCompleteSettingsElastic
            {
                Confidence = autoComplete.Confidence,
                Count = autoComplete.Count,
                MaximumErrors = autoComplete.MaximumErrors,
            };
            return model;
        }

        public static SearchSettings ToSearchSettingsModel(this SearchSettingsElastic search)
        {
            var model = new SearchSettings
            {
                Count = search.Count,
                CutOffFrequency = search.CutOffFrequency,
                Filter = search.Filter?.ToFilterModel(),
                Fuzziness = search.Fuzziness,
                ResponseFieldList = search.ResponseFieldList,
                SearchFieldList = search.SearchFieldList,
                Type = (SearchTypeEnum)search.Type,
                Weights = search.Weights?.Select(s => s.ToWeightModel()).ToList(),
                Operator = (LogicalOperatorEnum)search.Operator
            };
            return model;
        }

        public static SearchSettingsElastic ToSearchSettingsElastic(this SearchSettings search)
        {
            var model = new SearchSettingsElastic
            {
                Count = search.Count,
                CutOffFrequency = search.CutOffFrequency,
                Filter = search.Filter?.ToFilterElastic(),
                Fuzziness = search.Fuzziness,
                ResponseFieldList = search.ResponseFieldList,
                SearchFieldList = search.SearchFieldList,
                Type = (int)search.Type,
                Weights = search.Weights?.Select(s => s.ToWeightElastic()).ToList(),
                Operator = (int)search.Operator
            };
            return model;
        }

        public static ClassifierSettings ToClassifierSettingsModel(this ClassifierSearchSettingsElastic classifier)
        {
            var model = new ClassifierSettings
            {
                Id = classifier.Id,
                Count = classifier.Count
            };
            return model;
        }

        public static ClassifierSearchSettingsElastic ToClassifierSearchSettingsElastic(this ClassifierSettings classifier)
        {
            var model = new ClassifierSearchSettingsElastic
            {
                Id = classifier.Id,
                Count = classifier.Count
            };
            return model;
        }


        public static SearchPrepareSettings ToSearchPrepareSettingsModel(this SearchSettingsWrapperElastic searchWrapper)
        {
            var model = new SearchPrepareSettings
            {
                DataSetName = searchWrapper.DataSetName
            };
            return model;
        }

        public static SearchActivateSettings ToSearchActivateSettingsModel (this SearchSettingsWrapperElastic searchWrapper)
        {
            var model = new SearchActivateSettings {
                AutoCompleteSettings = searchWrapper.AutoCompleteSettings?.ToAutoCompleteSettingsModel(),
                ClassifierSettings = searchWrapper.ClassifierSettings?.ToClassifierSettingsModel(),
                Count = searchWrapper.Count,
                SearchSettings = searchWrapper.SearchSettings?.ToSearchSettingsModel()
            };
            return model;
        }

        #endregion
    }
}
