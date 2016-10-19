using System.Linq;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Slamby.API.Filters;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Services;
using Slamby.Common.Services.Interfaces;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers.Documents
{

    [Route("api/Documents/Move")]
    [SwaggerGroup("Document")]
    [SwaggerResponseRemoveDefaults]
    [DataSetNameFilter]
    public class DocumentsMoveController : BaseController
    {
        readonly ParallelService parallelService;
        readonly IDocumentService documentService;
        readonly IGlobalStoreManager globalStore;
        readonly string dataSetName;

        public DocumentsMoveController(IDocumentService documentService, ParallelService parallelService, 
            IGlobalStoreManager globalStore, IDataSetSelector dataSetSelector)
        {
            this.dataSetName = dataSetSelector.DataSetName;
            this.globalStore = globalStore;
            this.documentService = documentService;
            this.parallelService = parallelService;
        }

        [HttpPost]
        [SwaggerOperation("MoveDocuments")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status409Conflict, "", typeof(ErrorsModel))]
        public IActionResult Post([FromBody]DocumentMoveSettings moveSettings)
        {
            if (moveSettings.DocumentIdList == null || !moveSettings.DocumentIdList.Any())
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, DocumentResources.EmptyIdListSpecified);
            }

            if (!globalStore.DataSets.IsExist(moveSettings.TargetDataSetName))
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest,
                    string.Format(DataSetResources.DataSet_0_IsNotFound, moveSettings.TargetDataSetName));
            }

            var result = documentService.Move(dataSetName, moveSettings.DocumentIdList, moveSettings.TargetDataSetName, parallelService.ParallelLimit);
            if (result.IsFailure)
            {
                return HttpErrorResult(StatusCodes.Status409Conflict, result.Error);
            }

            return new HttpStatusCodeResult(StatusCodes.Status200OK);
        }
    }
}