using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Filters;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;
using System.Collections.Generic;
using Slamby.Common.Services.Interfaces;

namespace Slamby.API.Controllers
{
    [Route("api/Documents/Sample")]
    [SwaggerGroup("Document")]
    [SwaggerResponseRemoveDefaults]
    [DataSetNameFilter]
    public class DocumentsSampleController : BaseController
    {
        readonly IDocumentService documentService;
        readonly string dataSetName;

        public DocumentsSampleController(IDocumentService documentService, IDataSetSelector dataSetSelector)
        {
            this.dataSetName = dataSetSelector.DataSetName;
            this.documentService = documentService;
        }

        [HttpPost]
        [SwaggerOperation("GetSampleDocuments")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(PaginatedList<object>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        public IActionResult Post([FromBody]DocumentSampleSettings sampleSettings)
        {
            if (!(sampleSettings.Size > 0 || sampleSettings.Percent > 0))
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, DocumentResources.ValidSampleSizeOrPercentIsRequired);
            }

            if (sampleSettings.FieldList != null)
            {
                var validateResult = documentService.ValidateFieldFilterFields(dataSetName, sampleSettings.FieldList);
                if (validateResult.IsFailure)
                {
                    return HttpErrorResult(StatusCodes.Status400BadRequest, validateResult.Error);
                }
            }

            var results = sampleSettings.Size > 0 
                ? documentService.Sample(dataSetName, sampleSettings.Id, sampleSettings.TagIdList, sampleSettings.Size, sampleSettings?.FieldList) 
                : documentService.Sample(dataSetName, sampleSettings.Id, sampleSettings.TagIdList, sampleSettings.Percent, sampleSettings?.FieldList);

            return new OkObjectResult(results);
        }
    }
}