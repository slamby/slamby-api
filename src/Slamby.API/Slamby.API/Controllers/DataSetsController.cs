using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Newtonsoft.Json;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Services;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/[controller]")]
    [SwaggerGroup("DataSet")]
    [SwaggerResponseRemoveDefaults]
    public class DataSetsController : BaseController
    {
        readonly DataSetService dataSetService;
        readonly IDocumentService documentService;

        public DataSetsController(DataSetService dataSetService, IDocumentService documentService)
        {
            this.documentService = documentService;
            this.dataSetService = dataSetService;
        }

        // GET: api/datasets
        [HttpGet]
        [SwaggerOperation("GetDataSets")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<DataSet>))]
        public IActionResult Get()
        {
            var dataSets = dataSetService.Get();

            return new HttpOkObjectResult(dataSets);
        }

        [HttpGet("{name}")]
        [SwaggerOperation("GetDataSet")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(DataSet))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Get(string name)
        {
            dataSetService.ThrowIfDataSetIsBusy(name);
            var validationResultString = DataSetService.ValidateDataSetName(name);
            if (validationResultString.Any())
                return HttpErrorResult(StatusCodes.Status400BadRequest, validationResultString);

            if (!dataSetService.IsExists(name))
            {
                return HttpErrorResult(StatusCodes.Status404NotFound,
                    string.Format(DataSetResources.DataSet_0_IsNotFound, name));
            }

            var dataSet = dataSetService.Get(name);

            return new HttpOkObjectResult(dataSet);
        }

        [HttpPost]
        [SwaggerOperation("CreateDataSet")]
        [SwaggerResponse(StatusCodes.Status201Created)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status409Conflict, "", typeof(ErrorsModel))]
        public IActionResult Post([FromBody]DataSet dataSet)
        {
            dataSetService.ThrowIfDataSetIsBusy(dataSet.Name);
            if (dataSetService.IsExists(dataSet.Name))
            {
                return HttpErrorResult(StatusCodes.Status409Conflict,
                    string.Format(DataSetResources.DataSet_0_IsAlreadyExist, dataSet.Name));
            }

            // In rare cases it could be string instead of object
            if (dataSet.SampleDocument is string)
            {
                dataSet.SampleDocument = JsonConvert.DeserializeObject((string)dataSet.SampleDocument);
            }

            if (dataSet.SampleDocument == null)
            {
                return HttpBadRequest(ErrorsModel.Create(DataSetResources.SampleDocumentIsEmpty));
            }

            var validateResult = documentService.ValidateSampleDocument(dataSet);
            if (validateResult.IsFailure)
            {
                return HttpBadRequest(ErrorsModel.Create(validateResult.Error));
            }

            dataSetService.Create(dataSet, withSchema: false);

            return new HttpStatusCodeResult(StatusCodes.Status201Created);
        }

        [HttpPost("Schema")]
        [SwaggerOperation("CreateDataSetSchema")]
        [SwaggerResponse(StatusCodes.Status201Created)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status409Conflict, "", typeof(ErrorsModel))]
        public IActionResult PostWithSchema([FromBody]DataSet dataSet, [FromServices]DataSetSchemaValidatorService jsonValidator)
        {
            dataSetService.ThrowIfDataSetIsBusy(dataSet.Name);
            if (dataSetService.IsExists(dataSet.Name))
            {
                return HttpErrorResult(StatusCodes.Status409Conflict,
                    string.Format(DataSetResources.DataSet_0_IsAlreadyExist, dataSet.Name));
            }

            // In rare cases it could be string instead of object
            if (dataSet.Schema is string)
            {
                dataSet.Schema = JsonConvert.DeserializeObject((string)dataSet.Schema);
            }

            if (dataSet.Schema == null)
            {
                return HttpBadRequest(ErrorsModel.Create(DataSetResources.SchemaIsEmpty));
            }

            var validateResult = jsonValidator.Validate(dataSet.Schema);
            if (validateResult.Any())
            {
                return HttpBadRequest(ErrorsModel.Create(validateResult.Select(error =>
                    string.Format(DocumentResources.JsonSchemaValidationError_0, error)
                )));
            }

            var validateSchemaResult = documentService.ValidateSchema(dataSet);
            if (validateSchemaResult.IsFailure)
            {
                return HttpBadRequest(ErrorsModel.Create(validateSchemaResult.Error));
            }

            dataSetService.Create(dataSet, withSchema: true);

            return new HttpStatusCodeResult(StatusCodes.Status201Created);
        }

        [HttpPut("{existingName}")]
        [SwaggerOperation("UpdateDataSet")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status304NotModified)]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status409Conflict, "", typeof(ErrorsModel))]
        public IActionResult Put(string existingName, [FromBody]DataSetUpdate dataSetUpdate)
        {
            var validationResultString = DataSetService.ValidateDataSetName(existingName);
            if (validationResultString.Any())
                return HttpErrorResult(StatusCodes.Status400BadRequest, validationResultString);

            dataSetService.ThrowIfDataSetIsBusy(existingName);
            dataSetService.ThrowIfDataSetIsBusy(dataSetUpdate.Name);

            if (string.CompareOrdinal(existingName, dataSetUpdate.Name) == 0)
            {
                return new HttpStatusCodeResult(StatusCodes.Status304NotModified);
            }

            if (!dataSetService.IsExists(existingName))
            {
                return HttpErrorResult(StatusCodes.Status404NotFound,
                    string.Format(DataSetResources.DataSet_0_IsNotFound, existingName));
            }

            if (dataSetService.IsExists(dataSetUpdate.Name))
            {
                return HttpErrorResult(StatusCodes.Status409Conflict,
                    string.Format(DataSetResources.DataSet_0_IsAlreadyExist, dataSetUpdate.Name));
            }

            dataSetService.Update(existingName, dataSetUpdate.Name);

            return new HttpStatusCodeResult(StatusCodes.Status200OK);
        }

        // DELETE api/values/5
        [HttpDelete("{name}")]
        [SwaggerOperation("DeleteDataSet")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status406NotAcceptable, "", typeof(ErrorsModel))]
        public IActionResult Delete(string name)
        {
            dataSetService.ThrowIfDataSetIsBusy(name);

            var validationResultString = DataSetService.ValidateDataSetName(name);
            if (validationResultString.Any())
                return HttpErrorResult(StatusCodes.Status400BadRequest, validationResultString);

            if (!dataSetService.IsExists(name))
            {
                return HttpErrorResult(StatusCodes.Status404NotFound,
                    string.Format(DataSetResources.DataSet_0_IsNotFound, name));
            }
            if (dataSetService.HasServiceReference(name))
            {
                return HttpErrorResult(StatusCodes.Status406NotAcceptable, DataSetResources.DataSetNotAllowedToDeleteBecauseOfDependingService);
            }

            dataSetService.Delete(name);

            return new HttpStatusCodeResult(StatusCodes.Status200OK);
        }
    }
}
