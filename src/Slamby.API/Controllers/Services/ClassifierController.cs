using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Helpers;
using Slamby.API.Helpers.Services;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Helpers;
using Slamby.Elastic.Factories.Interfaces;
using Slamby.Elastic.Models;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models;
using Slamby.SDK.Net.Models.Enums;
using Slamby.SDK.Net.Models.Services;
using Swashbuckle.SwaggerGen.Annotations;
using Slamby.API.Filters;

namespace Slamby.API.Controllers
{
    [Route("api/Services/Classifier")]
    [SwaggerGroup("ClassifierService")]
    [SwaggerResponseRemoveDefaults]
    public class ClassifierController : BaseController
    {
        readonly ServiceQuery serviceQuery;
        readonly ClassifierServiceHandler classifierHandler;
        readonly ServiceHandler serviceHandler;
        readonly ProcessHandler processHandler;
        readonly IQueryFactory queryFactory;

        public IGlobalStoreManager GlobalStore { get; set; }

        public ClassifierController(ServiceQuery serviceQuery, ClassifierServiceHandler classifierHandler, ProcessHandler processHandler, 
            IQueryFactory queryFactory, IGlobalStoreManager globalStore, ServiceHandler serviceHandler)
        {
            GlobalStore = globalStore;
            this.queryFactory = queryFactory;
            this.processHandler = processHandler;
            this.classifierHandler = classifierHandler;
            this.serviceQuery = serviceQuery;
            this.serviceHandler = serviceHandler;
        }

        [HttpGet("{id}")]
        [SwaggerOperation("ClassifierGetService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(ClassifierService))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound)]
        public IActionResult Get(string id)
        {
            var service = classifierHandler.Get(id);
            if (service == null)
            {
                return new StatusCodeResult(StatusCodes.Status404NotFound);
            }
            if (service.Type != (int)ServiceTypeEnum.Classifier)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Classifier"));
            }
            return new OkObjectResult(service);
        }

        [HttpPost("{id}/Prepare")]
        [SwaggerOperation("ClassifierPrepareService")]
        [SwaggerResponse(StatusCodes.Status202Accepted, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        [ServiceFilter(typeof(DiskSpaceLimitFilter))]
        public IActionResult Prepare(string id, [FromBody]ClassifierPrepareSettings classifierPrepareSettings)
        {
            //SERVICE VALIDATION
            var service = classifierHandler.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Classifier)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Classifier"));
            }
            if (service.Status != (int)ServiceStatusEnum.New)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithNewStatusCanBePrepared);
            }

            //DATASET VALIDATION
            if (!GlobalStore.DataSets.IsExist(classifierPrepareSettings.DataSetName))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, 
                    string.Format(ServiceResources.DataSet_0_NotFound, classifierPrepareSettings.DataSetName));
            }

            var globalStoreDataSet = GlobalStore.DataSets.Get(classifierPrepareSettings.DataSetName);
            var dataSet = globalStoreDataSet.DataSet;

            //NGRAM COUNT LIST VALIDATION
            var nGramResult = CommonValidators.ValidateNGramList(classifierPrepareSettings.NGramList, dataSet.NGramCount);
            if (nGramResult.IsFailure)
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, nGramResult.Error);
            }

            //TAGS VALIDATION
            var tagQuery = queryFactory.GetTagQuery(dataSet.Name);
            List<TagElastic> tags;
            if (classifierPrepareSettings?.TagIdList?.Any() == true)
            {
                tags = tagQuery.Get(classifierPrepareSettings.TagIdList).ToList();
                if (tags.Count < classifierPrepareSettings.TagIdList.Count)
                {
                    var missingTagIds = classifierPrepareSettings.TagIdList.Except(tags.Select(t => t.Id)).ToList();
                    return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, 
                        string.Format(ServiceResources.TheFollowingTagIdsNotExistInTheDataSet_0, string.Join(", ", missingTagIds)));
                }
            }
            else
            {
                tags = tagQuery.GetAll().Items.Where(i => i.IsLeaf).ToList();
            }
            
            //SAVE SETTINGS TO ELASTIC
            var serviceSettings = new ClassifierSettingsElastic {
                DataSetName = globalStoreDataSet.IndexName,
                ServiceId = service.Id,
                NGramList = classifierPrepareSettings.NGramList,
                Tags = tags,
                CompressSettings = CompressHelper.ToCompressSettingsElastic(classifierPrepareSettings.CompressSettings, classifierPrepareSettings.CompressLevel)
            };
            serviceQuery.IndexSettings(serviceSettings);

            var process = processHandler.Create(
                ProcessTypeEnum.ClassifierPrepare,
                service.Id, classifierPrepareSettings,
                string.Format(ServiceResources.Preparing_0_Service_1, ServiceTypeEnum.Classifier, service.Name));

            service.ProcessIdList.Add(process.Id);
            serviceHandler.Update(service.Id, service);

            processHandler.Start(process, (tokenSource) => classifierHandler.Prepare(process.Id, serviceSettings, tokenSource.Token));

            return new HttpStatusCodeWithObjectResult(StatusCodes.Status202Accepted, process.ToProcessModel());
        }

        [HttpPost("{id}/Activate")]
        [SwaggerOperation("ClassifierActivateService")]
        [SwaggerResponse(StatusCodes.Status202Accepted, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Activate(string id, [FromBody]ClassifierActivateSettings classifierActivateSettings)
        {
            //SERVICE VALIDATION
            var service = classifierHandler.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != ServiceTypeEnum.Classifier)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Classifier"));
            }
            if (service.Status != ServiceStatusEnum.Prepared)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithPreparedStatusCanBeActivated);
            }
            var classifierSettings = serviceQuery.GetSettings<ClassifierSettingsElastic>(service.Id);
            service.Status = ServiceStatusEnum.Active;

            if (classifierActivateSettings == null)
            {
                if (classifierSettings.ActivatedTagIdList == null ||
                    classifierActivateSettings.NGramList == null)
                {
                    return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.EmptyActivateSettings);
                }
            }
            else
            {
                //NGRAM LIST VALIDATION
                if (classifierActivateSettings.NGramList.Except(classifierSettings.NGramList).Count() > 0)
                {
                    return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.NGramCountWasntPrepared);
                }
                //TAGS VALIDATION
                var preparedTagIds = classifierSettings.Tags.Select(t => t.Id).ToList();
                List<string> tagIds;
                if (classifierActivateSettings?.TagIdList?.Any() == true)
                {
                    tagIds = preparedTagIds.Intersect(classifierActivateSettings.TagIdList).ToList();
                    if (tagIds.Count < classifierActivateSettings.TagIdList.Count)
                    {
                        var missingTagIds = classifierActivateSettings.TagIdList.Except(preparedTagIds).ToList();
                        return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                            string.Format(ServiceResources.TheFollowingTagIdsWereNotPrepared_0, string.Join(", ", missingTagIds)));
                    }
                }
                else
                {
                    tagIds = preparedTagIds;
                }

                var emphasizedTagIds = new List<string>();
                if (classifierActivateSettings?.EmphasizedTagIdList?.Any() == true)
                {
                    emphasizedTagIds = preparedTagIds.Intersect(classifierActivateSettings.EmphasizedTagIdList).ToList();
                    if (emphasizedTagIds.Count < classifierActivateSettings.EmphasizedTagIdList.Count)
                    {
                        var missingTagIds = classifierActivateSettings.EmphasizedTagIdList.Except(preparedTagIds).ToList();
                        return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, 
                            string.Format(ServiceResources.TheFollowingEmphasizedTagIdsWereNotPrepared_0, string.Join(", ", missingTagIds)));
                    }
                }

                classifierSettings.ActivatedNGramList = classifierActivateSettings.NGramList;
                classifierSettings.ActivatedTagIdList = tagIds;
                classifierSettings.EmphasizedTagIdList = emphasizedTagIds;
            }

            var process = processHandler.Create(
                ProcessTypeEnum.ClassifierActivate,
                service.Id,
                classifierSettings,
                string.Format(ServiceResources.Activating_0_Service_1, ServiceTypeEnum.Classifier, service.Name));

            service.ProcessIdList.Add(process.Id);
            serviceHandler.Update(service.Id, service);
            serviceQuery.IndexSettings(classifierSettings);

            processHandler.Start(process, (tokenSource) => classifierHandler.Activate(process.Id, classifierSettings, tokenSource.Token));

            return new HttpStatusCodeWithObjectResult(StatusCodes.Status202Accepted, process.ToProcessModel());
        }

        [HttpPost("{id}/Deactivate")]
        [SwaggerOperation("ClassifierDeactivateService")]
        [SwaggerResponse(StatusCodes.Status200OK, "")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Deactivate(string id) {
            //SERVICE VALIDATION
            var service = classifierHandler.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != ServiceTypeEnum.Classifier)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Classifier"));
            }
            if (service.Status != ServiceStatusEnum.Active)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithActiveStatusCanBeDeactivated);
            }

            classifierHandler.Deactivate(service.Id);

            service.Status = ServiceStatusEnum.Prepared;
            serviceHandler.Update(service.Id, service);

            return new OkResult();
        }

        [HttpPost("{id}/Recommend")]
        [SwaggerOperation("ClassifierRecommendService")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<ClassifierRecommendationResult>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        public IActionResult Recommend(string id, [FromBody]ClassifierRecommendationRequest request)
        {
            // If Id is Alias, translate to Id
            if (GlobalStore.ServiceAliases.IsExist(id))
            {
                id = GlobalStore.ServiceAliases.Get(id);
            }
            if (!GlobalStore.ActivatedClassifiers.IsExist(id))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.ServiceNotExistsOrNotActivated, ServiceTypeEnum.Classifier));
            }
            if (request.ParentTagIdList?.Any() == true &&
                request.ParentTagIdList.Any(i => !GlobalStore.ActivatedClassifiers.Get(id).ClassifierParentTagIds.ContainsKey(i)))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.NotAllParentTagExists);
            }
            var results = classifierHandler.Recommend(id, request.Text, request.Count, request.UseEmphasizing, request.NeedTagInResult, request.ParentTagIdList);
            return new OkObjectResult(results);
        }

        [HttpPost("{id}/ExportDictionaries")]
        [SwaggerOperation("ClassifierExportDictionaries")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        [ServiceFilter(typeof(DiskSpaceLimitFilter))]
        public IActionResult ExportDictionaries(string id, [FromBody]ExportDictionariesSettings settings, [FromServices]UrlProvider urlProvider)
        {
            //SERVICE VALIDATION
            var service = classifierHandler.Get(id);
            if (service == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, ServiceResources.InvalidIdNotExistingService);
            }
            if (service.Type != (int)ServiceTypeEnum.Classifier)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, string.Format(ServiceResources.InvalidServiceTypeOnly_0_ServicesAreValidForThisRequest, "Classifier"));
            }
            if (service.Status != ServiceStatusEnum.Active && service.Status != ServiceStatusEnum.Prepared)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ServiceResources.InvalidStatusOnlyTheServicesWithPreparedOrActiveStatusCanBeExported);
            }

            var classifierSettings = serviceQuery.GetSettings<ClassifierSettingsElastic>(service.Id);

            //TAGS VALIDATION
            var tagQuery = queryFactory.GetTagQuery(classifierSettings.DataSetName);
            List<string> tagIds;
            if (settings?.TagIdList?.Any() == true)
            {
                tagIds = classifierSettings.Tags.Select(t => t.Id).Intersect(settings.TagIdList).ToList();
                if (tagIds.Count < settings.TagIdList.Count)
                {
                    var missingTagIds = settings.TagIdList.Except(tagIds).ToList();
                    return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest,
                        string.Format(ServiceResources.TheFollowingTagIdsNotExistInTheService_0, string.Join(", ", missingTagIds)));
                }
            }
            else
            {
                tagIds = classifierSettings.Tags.Select(t => t.Id).ToList();
            }

            //NGRAM COUNT LIST VALIDATION
            var nGramResult = CommonValidators.ValidateServiceNGramList(classifierSettings.NGramList, settings.NGramList);
            if (nGramResult.IsFailure)
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, nGramResult.Error);
            }

            var process = processHandler.Create(
                ProcessTypeEnum.ClassifierExportDictionaries,
                service.Id, settings,
                string.Format(ServiceResources.ExportingDictionariesFrom_0_Service_1, ServiceTypeEnum.Classifier, service.Name));

            service.ProcessIdList.Add(process.Id);
            serviceHandler.Update(service.Id, service);

            processHandler.Start(process, (tokenSource) => 
                classifierHandler.ExportDictionaries(process.Id, classifierSettings, tagIds, settings.NGramList, tokenSource.Token, urlProvider.GetHostUrl()));

            return new HttpStatusCodeWithObjectResult(StatusCodes.Status202Accepted, process.ToProcessModel());
        }
    }
}
