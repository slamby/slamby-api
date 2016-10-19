using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Slamby.API.Helpers;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.Common.Helpers;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models;
using Slamby.SDK.Net.Models.Enums;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/[controller]")]
    [SwaggerGroup("Process")]
    [SwaggerResponseRemoveDefaults]
    public class ProcessesController : BaseController
    {
        readonly ProcessQuery processQuery;
        readonly ProcessHandler processHandler;

        public ProcessesController(ProcessQuery processQuery, ProcessHandler processHandler)
        {
            this.processHandler = processHandler;
            this.processQuery = processQuery;
        }

        [HttpGet]
        [SwaggerOperation("GetProcesses")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<Process>))]
        public IActionResult Get([FromQuery]bool allStatus = false)
        {
            var processes = allStatus 
                ? processQuery.GetAll() 
                : processQuery.GetActives();

            return new HttpOkObjectResult(processes.Select(ModelHelper.ToProcessModel));
        }

        [HttpGet("{id}")]
        [SwaggerOperation("GetProcess")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(Process))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Get(string id)
        {
            var process = processQuery.Get(id);
            if (process == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound,
                    string.Format(ProcessResources.ProcessWithId_0_DoesNotFound, id));
            }

            return new HttpOkObjectResult(process.ToProcessModel());
        }

        [HttpPost("{id}/Cancel")]
        [SwaggerOperation("CancelProcess")]
        [SwaggerResponse(StatusCodes.Status202Accepted)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Cancel(string id)
        {
            var process = processQuery.Get(id);
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
            return new HttpStatusCodeResult(StatusCodes.Status202Accepted);
        }
    }
}
