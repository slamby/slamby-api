using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Filters;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services;
using Slamby.Common.Services;
using Slamby.Common.Services.Interfaces;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/Tags/Bulk")]
    [SwaggerGroup("Tag")]
    [DataSetNameFilter]
    public class TagsBulkController : BaseController
    {
        readonly TagService tagService;
        readonly IHttpContextAccessor contextAccessor;
        readonly ParallelService parallelService;
        readonly string dataSetName;

        public TagsBulkController(TagService tagService, IHttpContextAccessor contextAccessor, 
            ParallelService parallelService, IDataSetSelector dataSetSelector)
        {
            this.dataSetName = dataSetSelector.DataSetName;
            this.parallelService = parallelService;
            this.contextAccessor = contextAccessor;
            this.tagService = tagService;
        }

        /// <summary>
        /// Updates the whole tag tree if input tags list consistent (no errors)
        /// </summary>
        /// <param name="settings">Always gets the full tag tree</param>
        /// <returns></returns>
        [HttpPost]
        [SwaggerOperation("BulkTags")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(BulkResults))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status411LengthRequired, "", typeof(ErrorsModel))]
        public IActionResult Post([FromBody]TagBulkSettings settings)
        {
            var contentLength = contextAccessor.HttpContext.Request.ContentLength;
            if (contentLength == null)
            {
                return HttpErrorResult(StatusCodes.Status411LengthRequired, GlobalResources.ContentLengthIsNotSet);
            }

            // Trim Id, ParentId for better match
            var tags = settings.Tags
                .Where(t => t != null)
                .Select(t => tagService.TrimTag(t))
                .ToList();

            if (!tags.Any())
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, TagResources.NoOperationCanBeDoneOnEmptyTagList);
            }

            // check empty ids
            if (tags.Any(t => string.IsNullOrEmpty(t.Id)))
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, TagResources.TagIdIsEmpty);
            }

            // check duplicate ids
            var duplicateIds = tags.GroupBy(t => t.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateIds.Any())
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest,
                    duplicateIds.Select(id => string.Format(TagResources.DuplicateIdsFound_0, id)));
            }

            var bulkResult = tagService.BulkCreate(dataSetName, tags, parallelService.ParallelLimit, contentLength.Value);
            return HttpObjectResult(StatusCodes.Status200OK, bulkResult.Value);
        }
    }
}
