using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Filters;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Services;
using Slamby.Common.Services.Interfaces;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/Documents/Bulk")]
    [SwaggerGroup("Document")]
    [SwaggerResponseRemoveDefaults]
    [DataSetNameFilter]
    public class DocumentsBulkController : BaseController
    {
        readonly IDocumentService documentService;
        readonly ParallelService parallelService;
        readonly IHttpContextAccessor contextAccessor;
        readonly string dataSetName;

        public DocumentsBulkController(IDocumentService documentService, ParallelService parallelService, 
            IHttpContextAccessor contextAccessor, IDataSetSelector dataSetSelector)
        {
            this.dataSetName = dataSetSelector.DataSetName;
            this.contextAccessor = contextAccessor;
            this.parallelService = parallelService;
            this.documentService = documentService;
        }

        [HttpPost]
        [SwaggerOperation("BulkDocuments")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(BulkResults))]
        [SwaggerResponse(StatusCodes.Status411LengthRequired, "", typeof(ErrorsModel))]
        public IActionResult Post([FromBody]DocumentBulkSettings settings)
        {
            var contentLength = contextAccessor.HttpContext.Request.ContentLength;
            if (contentLength == null)
            {
                return HttpErrorResult(StatusCodes.Status411LengthRequired, GlobalResources.ContentLengthIsNotSet);
            }

            var results = documentService.Bulk(
                dataSetName,
                settings.Documents,
                contentLength.Value,
                parallelService.ParallelLimit);

            return HttpObjectResult(StatusCodes.Status200OK, results);
        }
    }
}