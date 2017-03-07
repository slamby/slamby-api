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

        public static ServiceElastic ToServiceElastic(this Service service) {
            return new ServiceElastic
            {
                Alias = service.Alias,
                Description = service.Description,
                Id = service.Id,
                Name = service.Name,
                ProcessIdList = service.ProcessIdList,
                Status = (int)service.Status,
                Type = (int)service.Type
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

        public static Order ToOrderModel(this OrderElastic order)
        {
            var model = new Order
            {
                OrderByField = order.OrderByField,
                OrderDirection = (OrderDirectionEnum)order.OrderDirection
            };
            return model;
        }

        public static OrderElastic ToOrderElastic(this Order order)
        {
            var model = new OrderElastic
            {
                OrderByField = order.OrderByField,
                OrderDirection = (int)order.OrderDirection
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

        public static AutoCompleteSettingsElastic ToAutoCompleteSettingsElastic(this AutoCompleteSettings autoComplete, AutoCompleteSettingsElastic original = null)
        {
            var model = new AutoCompleteSettingsElastic
            {
                Confidence = autoComplete.Confidence.HasValue ? autoComplete.Confidence.Value : (double)original?.Confidence,
                Count = autoComplete.Count.HasValue ? autoComplete.Count.Value : (int)original?.Count,
                MaximumErrors = autoComplete.MaximumErrors.HasValue ? autoComplete.MaximumErrors.Value : (double)original?.MaximumErrors,
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
                Operator = (LogicalOperatorEnum)search.Operator,
                UseDefaultFilter = search.UseDefaultFilter,
                UseDefaultWeights = search.UseDefaultWeights,
                Order = search.Order?.ToOrderModel()
            };
            return model;
        }

        public static SearchSettingsElastic ToSearchSettingsElastic(this SearchSettings search, SearchSettingsElastic original = null, bool emptyDefaults = false)
        {
            var model = new SearchSettingsElastic
            {
                Count = search.Count.HasValue ? search.Count.Value : (int)original?.Count,
                CutOffFrequency = search.CutOffFrequency.HasValue ? search.CutOffFrequency.Value : (double)original?.CutOffFrequency,
                Filter = search.Filter != null ? search.Filter.ToFilterElastic() : original?.Filter,
                Fuzziness = search.Fuzziness.HasValue ? search.Fuzziness.Value : (int)original?.Fuzziness,
                ResponseFieldList = search.ResponseFieldList != null ? search.ResponseFieldList : original?.ResponseFieldList,
                SearchFieldList = search.SearchFieldList != null ? search.SearchFieldList : original?.SearchFieldList,
                Type = search.Type.HasValue ? (int)search.Type.Value : (int)original?.Type,
                Weights = search.Weights != null ? search.Weights.Select(s => s.ToWeightElastic()).ToList() : original?.Weights,
                Operator = search.Operator.HasValue ? (int)search.Operator.Value : (int)original?.Operator,
                UseDefaultFilter = search.UseDefaultFilter.HasValue ? search.UseDefaultFilter.Value : (bool)original?.UseDefaultFilter,
                UseDefaultWeights = search.UseDefaultWeights.HasValue ? search.UseDefaultWeights.Value : (bool)original?.UseDefaultFilter,
                Order = search.Order != null ? search.Order.ToOrderElastic() : original?.Order,
            };
            if (emptyDefaults)
            {
                if (search.UseDefaultFilter == false && search.Filter == null) model.Filter = null;
                if (search.UseDefaultWeights == false && search.Weights == null) model.Weights = null;
            }
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

        public static ClassifierSearchSettingsElastic ToClassifierSearchSettingsElastic(this ClassifierSettings classifier, ClassifierSearchSettingsElastic original = null)
        {
            var model = new ClassifierSearchSettingsElastic
            {
                Id = !string.IsNullOrEmpty(classifier.Id) ? classifier.Id : original?.Id,
                Count = classifier.Count.HasValue ? classifier.Count.Value : (int)original?.Count,
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
                SearchSettings = searchWrapper.SearchSettings?.ToSearchSettingsModel()
            };
            return model;
        }

        #endregion
    }
}
