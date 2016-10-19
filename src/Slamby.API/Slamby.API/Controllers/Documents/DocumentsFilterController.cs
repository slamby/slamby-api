using System.Linq;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Slamby.API.Filters;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Exceptions;
using Slamby.Common.Services.Interfaces;
using Slamby.SDK.Net.Models;
using Slamby.SDK.Net.Models.Enums;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/Documents/Filter")]
    [SwaggerGroup("Document")]
    [SwaggerResponseRemoveDefaults]
    [DataSetNameFilter]
    public class DocumentsFilterController : BaseController
    {
        readonly IDocumentService documentService;
        readonly string dataSetName;

        public DocumentsFilterController(IDocumentService documentService, IDataSetSelector dataSetSelector)
        {
            this.dataSetName = dataSetSelector.DataSetName;
            this.documentService = documentService;
        }

        [HttpPost("{scrollId?}")]
        [SwaggerOperation("GetFilteredDocuments")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(PaginatedList<object>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        public IActionResult Post([FromBody]DocumentFilterSettings filterSettings = null, [FromRoute]string scrollId = null)
        {
            if (filterSettings == null && scrollId == null)
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest,
                    DocumentResources.FilterSettingsOrScrollIdParameterMustHasValue);
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(scrollId))
                {
                    return new HttpOkObjectResult(documentService.GetScrolled(dataSetName, scrollId));
                }

                if (!string.IsNullOrEmpty(filterSettings.Order?.OrderByField))
                {
                    var orderByFieldResult = documentService.ValidateOrderByField(dataSetName, filterSettings.Order.OrderByField);
                    if (orderByFieldResult.IsFailure)
                    {
                        return HttpErrorResult(StatusCodes.Status400BadRequest, orderByFieldResult.Error);
                    }
                }

                if (filterSettings.FieldList != null)
                {
                    var validateResult = documentService.ValidateFieldFilterFields(dataSetName, filterSettings.FieldList);
                    if (validateResult.IsFailure)
                    {
                        return HttpErrorResult(StatusCodes.Status400BadRequest, validateResult.Error);
                    }
                }

                var paginatedList =
                    documentService.Filter(
                        dataSetName,
                        filterSettings?.Filter?.Query,
                        filterSettings?.Filter?.TagIdList,
                        filterSettings.Pagination.Limit,
                        filterSettings?.Order?.OrderByField,
                        filterSettings?.Order?.OrderDirection == OrderDirectionEnum.Desc,
                        filterSettings?.FieldList);

                return new HttpOkObjectResult(paginatedList);
            }
            catch (ElasticSearchException ex) 
                when (ex.ServerError.Error.Type == "search_phase_execution_exception" &&
                    ex.ServerError.Error.RootCause.Any(e => e.Type == "number_format_exception"))
            {
                var errors = 
                        ex.ServerError.Error.RootCause
                            .Where(c => c.Type == "number_format_exception")
                            .Select(c => c.Reason.Replace("For input string: ", ""))
                            .Select(input => string.Format(DocumentResources.InvalidNumericFilterValue, input));

                return HttpErrorResult(StatusCodes.Status400BadRequest, errors);
            }
        }
    }
}