using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Filters;
using Slamby.API.Helpers;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Services.Interfaces;
using Slamby.Elastic.Models;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/Tags/ExportWords")]
    [SwaggerGroup("Tag")]
    [DataSetNameFilter]
    [ServiceFilter(typeof(DiskSpaceLimitFilter))]
    public class TagsExportWordsController : BaseController
    {
        public string DataSetName { get; private set; }
        readonly TagService tagService;
        readonly IGlobalStoreManager globalStore;

        public TagsExportWordsController(TagService tagService, IDataSetSelector dataSetSelector, IGlobalStoreManager globalStore)
        {
            this.globalStore = globalStore;
            this.tagService = tagService;
            this.DataSetName = dataSetSelector.DataSetName;
        }

        [HttpPost]
        [SwaggerOperation("WordsExport")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        public IActionResult Post([FromBody]TagsExportWordsSettings settings)
        {
            //NGRAM COUNT LIST VALIDATION
            var nGramCount = globalStore.DataSets.Get(DataSetName).DataSet.NGramCount;
            var nGramResult = CommonValidators.ValidateNGramList(settings.NGramList, nGramCount);
            if (nGramResult.IsFailure)
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, nGramResult.Error);
            }

            //TAGS VALIDATION
            List<TagElastic> tags;
            if (settings.TagIdList == null)
            {
                tags = tagService.GetTagElasticLeafs(DataSetName);
            }
            else
            {
                tags = tagService.GetTagElastic(DataSetName, settings.TagIdList);
                if (tags.Count < settings.TagIdList.Count)
                {
                    var missingTagIds = settings.TagIdList.Except(tags.Select(t => t.Id)).ToList();
                    return HttpErrorResult(StatusCodes.Status400BadRequest, 
                        string.Format(ServiceResources.TheFollowingTagIdsNotExistInTheDataSet_0, string.Join(", ", missingTagIds)));
                }
            }

            var process = tagService.ExportWords(DataSetName, settings, tags);

            return HttpObjectResult(StatusCodes.Status202Accepted, process);
        }        
    }
}