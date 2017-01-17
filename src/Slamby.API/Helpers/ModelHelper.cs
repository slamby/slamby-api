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
        public static HighlightSettings ToHighlightSettingsModel(this HighlightSettingsElastic highlight)
        {
            var model = new HighlightSettings
            {
                PreTag = highlight.PreTag,
                PostTag = highlight.PostTag
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

        public static Filter ToFilterModel(this FilterElastic filter)
        {
            var model = new Filter
            {
                Query = filter.Query,
                TagIdList = filter.TagIdList
            };
            return model;
        }

        public static SearchFieldWeight ToSearchFieldWeightModel(this WeightElastic weight)
        {
            var model = new SearchFieldWeight
            {
                Field = weight.Query,
                Value = weight.Value
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

        public static AutoCompleteSettings ToAutoCompleteSettingsModel(this AutoCompleteSettingsElastic autoComplete)
        {
            var model = new AutoCompleteSettings
            {
                Confidence = autoComplete.Confidence,
                Count = autoComplete.Count,
                HighlightSettings = autoComplete.HighlightSettings.ToHighlightSettingsModel(),
                MaximumErrors = autoComplete.MaximumErrors,
                NGram = autoComplete.NGram
            };
            return model;
        }

        public static SearchSettings ToSearchSettingsModel(this SearchSettingsElastic search)
        {
            var model = new SearchSettings
            {
                Count = search.Count,
                CutOffFrequency = search.CutOffFrequency,
                Filter = search.Filter.ToFilterModel(),
                Fuzziness = search.Fuzziness,
                HighlightSettings = search.HighlightSettings.ToHighlightSettingsModel(),
                ResponseFieldList = search.ResponseFieldList,
                SearchFieldList = search.SearchFieldList,
                SearchFieldWeights = search.SearchFieldWeights.Select(s => s.ToSearchFieldWeightModel()).ToList(),
                Type = (SearchTypeEnum)search.Type,
                Weights = search.Weights.Select(s => s.ToWeightModel()).ToList()
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
                HighlightSettings = searchWrapper.HighlightSettings?.ToHighlightSettingsModel(),
                SearchSettings = searchWrapper.SearchSettings?.ToSearchSettingsModel()
            };
            return model;
        }

        #endregion
    }
}
