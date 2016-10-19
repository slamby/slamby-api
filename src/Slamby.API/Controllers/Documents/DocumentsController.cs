using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Slamby.API.Filters;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Helpers;
using Slamby.Common.Services.Interfaces;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/[controller]")]
    [SwaggerGroup("Document")]
    [SwaggerResponseRemoveDefaults]
    [DataSetNameFilter]
    public class DocumentsController : BaseController
    {
        readonly IDocumentService documentService;
        public string DataSetName { get; set; }

        public DocumentsController(IDocumentService documentService, 
            IDataSetSelector dataSetSelector)
        {
            this.DataSetName = dataSetSelector.DataSetName;
            this.documentService = documentService;
        }

        [HttpGet("{id}")]
        [SwaggerOperation("GetDocument")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(object))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Get(string id)
        {
            var documentElastic = documentService.Get(DataSetName, id);
            if (documentElastic == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound, 
                    string.Format(DocumentResources.DocumentWithId_0_DoesNotFound, id));
            }

            return new HttpOkObjectResult(documentElastic.DocumentObject);
        }

        [HttpPost]
        [SwaggerOperation("CreateDocument")]
        [SwaggerResponse(StatusCodes.Status201Created)]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status409Conflict, "", typeof(ErrorsModel))]
        public IActionResult Post([FromBody]object document)
        {
            var validateResult = documentService.ValidateDocument(DataSetName, document);
            if (validateResult.IsFailure)
            {
                return HttpBadRequest(ErrorsModel.Create(validateResult.Error));
            }

            var id = documentService.GetIdValue(DataSetName, document);
            if (documentService.IsExists(DataSetName, id))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status409Conflict,
                    string.Format(DocumentResources.DocumentWithId_0_IsAlreadyExist, id));
            }

            var indexResult = documentService.Index(DataSetName, document, id);
            if (indexResult.IsFailure)
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, indexResult.Error);
            }

            return new HttpStatusCodeResult(StatusCodes.Status201Created);
        }

        [HttpPut("{id}")]
        [SwaggerOperation("UpdateDocument")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(object))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Put(string id, [FromBody]object document)
        {
            var documentOriginal = documentService.Get(DataSetName, id);
            if (documentOriginal == null)
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound,
                    string.Format(DocumentResources.DocumentWithId_0_DoesNotFound, id));
            }

            var validateResult = documentService.ValidateUpdateDocument(DataSetName, document);
            if (validateResult.IsFailure)
            {
                return HttpBadRequest(ErrorsModel.Create(validateResult.Error));
            }

            var newId = documentService.GetIdValue(DataSetName, document)?? id;
            var updateResult = documentService.Update(DataSetName, id, documentOriginal, newId, document);
            if (updateResult.IsFailure)
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, updateResult.Error);
            }
            
            return new HttpOkObjectResult(documentService.Get(DataSetName, newId)?.DocumentObject);
        }

        /*
        // JsonPatchDocument not works with object type. The object will be null...

        [HttpPatch("{id}")]
        public IActionResult Patch(string id, [FromBody]JsonPatchDocument<object> document)
        {
            var docQuery = new DocumentQuery(DataSet.Name);
            var documentElastic = docQuery.Get(id);
            if (documentElastic == null)
            {
                return new HttpStatusCodeResult(StatusCodes.Status404NotFound);
            }
            document.ApplyTo(documentElastic.DocumentObject);
            var newId = ObjectAccessor.Create(document)[DataSet.IdField].ToString(); // ez nem jó a nested fields miatt
            documentElastic.Id = newId;
            documentElastic.ModifiedDate = System.DateTime.UtcNow;
            docQuery.Update(id, documentElastic);
            return new ObjectResult(documentElastic.DocumentObject);
        }
        */

        // DELETE api/documents/5
        [HttpDelete("{id}")]
        [SwaggerOperation("DeleteDocument")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        public IActionResult Delete(string id)
        {
            if (!documentService.IsExists(DataSetName, id))
            {
                return new HttpStatusCodeWithErrorResult(StatusCodes.Status404NotFound,
                    string.Format(DocumentResources.DocumentWithId_0_DoesNotFound, id));
            }
            documentService.Delete(DataSetName, id);
            return new HttpStatusCodeResult(StatusCodes.Status200OK);
        }
    }
}