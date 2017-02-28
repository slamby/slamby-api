using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Helpers;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.Common.Helpers;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models;
using Slamby.SDK.Net.Models.Enums;
using Swashbuckle.SwaggerGen.Annotations;
using Slamby.API.Services;
using Slamby.API.Services.Interfaces;

namespace Slamby.API.Controllers
{
    [Route("api/[controller]")]
    [SwaggerGroup("Process")]
    [SwaggerResponseRemoveDefaults]
    public class ProcessesController : BaseController
    {
        readonly ProcessQuery processQuery;
        readonly ProcessHandler processHandler;
        readonly IGlobalStoreManager globalStore;

        public ProcessesController(ProcessQuery processQuery, ProcessHandler processHandler, IGlobalStoreManager globalStore)
        {
            this.processHandler = processHandler;
            this.processQuery = processQuery;
            this.globalStore= globalStore;
        }

        [HttpGet]
        [SwaggerOperation("GetProcesses")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<Process>))]
        public IActionResult Get([FromQuery]bool allStatus = false, [FromQuery]bool allTime = false)
        {
            var processes = processQuery.GetAll(globalStore.InstanceId, !allStatus, allTime ? 0 : 30);
            return new OkObjectResult(processes.Select(ModelHelper.ToProcessModel));
        }

        [HttpGet("{id}")]
        [SwaggerOperation("GetProcess")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Get(string id)
        {
            var process = processQuery.Get(globalStore.InstanceId, id);
            if (process == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound,
                    string.Format(ProcessResources.ProcessWithId_0_DoesNotFound, id));
            }

            return new OkObjectResult(process.ToProcessModel());
        }

        [HttpPost("{id}/Cancel")]
        [SwaggerOperation("CancelProcess")]
        [SwaggerResponse(StatusCodes.Status202Accepted)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Cancel(string id)
        {
            var process = processQuery.Get(globalStore.InstanceId, id);
            if (process == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound,
                    string.Format(ProcessResources.ProcessWithId_0_DoesNotFound, id));
            }

            if (process.Status != (int)ProcessStatusEnum.InProgress)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status400BadRequest, ProcessResources.InvalidStatusOnlyProcessesWithInProgressCanBeCancelled);
            }
            processHandler.Cancel(process.Id);
            return new StatusCodeResult(StatusCodes.Status202Accepted);
        }
    }
}
