using System.Linq;
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

namespace Slamby.API.Controllers.Documents
{

    [Route("api/Documents/Move")]
    [SwaggerGroup("Document")]
    [SwaggerResponseRemoveDefaults]
    [DataSetNameFilter]
    [ServiceFilter(typeof(DiskSpaceLimitFilter))]
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
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
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
            var process = documentService.StartCopyOrMove(dataSetName, moveSettings, true, parallelService.ParallelLimit);
            return HttpObjectResult(StatusCodes.Status202Accepted, process);
        }
    }
}