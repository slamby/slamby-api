using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Filters;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Services.Interfaces;
using Slamby.Elastic.Queries;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/Tags/CleanDocuments")]
    [SwaggerGroup("Tag")]
    [DataSetNameFilter]
    public class TagsCleanDocumentsController : BaseController
    {
        readonly IDocumentService documentService;
        readonly TagQuery tagQuery;
        readonly IDataSetSelector dataSetSelector;

        public TagsCleanDocumentsController(IDocumentService documentService, TagQuery tagQuery,
            IDataSetSelector dataSetSelector)
        {
            this.dataSetSelector = dataSetSelector;
            this.tagQuery = tagQuery;
            this.documentService = documentService;
        }

        [HttpPost]
        [SwaggerOperation("CleanDocuments")]
        [SwaggerResponse(StatusCodes.Status200OK, "")]
        public IActionResult Post()
        {
            var tagIds = tagQuery.GetAll().Items.Select(i => i.Id).ToList();
            documentService.CleanDocuments(dataSetSelector.DataSetName, tagIds);

            return new StatusCodeResult(StatusCodes.Status200OK);
        }
    }
}
