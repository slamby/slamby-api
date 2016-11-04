using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Filters;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.API.Services;
using Slamby.Common.Services.Interfaces;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/[controller]")]
    [SwaggerGroup("Tag")]
    [SwaggerResponseRemoveDefaults]
    [DataSetNameFilter]
    public class TagsController : BaseController
    {
        readonly TagService tagService;

        public string DataSetName { get; private set; }

        public TagsController(TagService tagService, IDataSetSelector dataSetSelector)
        {
            this.tagService = tagService;
            this.DataSetName = dataSetSelector.DataSetName;
        }

        [HttpGet]
        [SwaggerOperation("GetTags")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(IEnumerable<Tag>))]
        public IActionResult Get([FromQuery]bool withDetails = false)
        {
            var tags = tagService.GetTagModels(DataSetName, withDetails);

            return new OkObjectResult(tags);
        }

        [HttpGet("{id}", Name = "GetTag")]
        [SwaggerOperation("GetTag")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(Tag))]
        [SwaggerResponse(StatusCodes.Status404NotFound, type: typeof(ErrorsModel))]
        public IActionResult Get(string id, [FromQuery]bool withDetails = false)
        {
            if (!tagService.IsExist(DataSetName, id))
            {
                return HttpErrorResult(StatusCodes.Status404NotFound,
                        string.Format(TagResources.TagWithId_0_DoesNotFound, id));
            }

            var tag = tagService.GetTagModel(DataSetName, id, withDetails);

            return new OkObjectResult(tag);
        }

        [HttpPost]
        [SwaggerOperation("CreateTag")]
        [SwaggerResponse(StatusCodes.Status201Created, type: typeof(Tag))]
        [SwaggerResponse(StatusCodes.Status409Conflict, type: typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status406NotAcceptable, type: typeof(ErrorsModel))]
        public IActionResult Post([FromBody]Tag tag)
        {
            tag = tagService.TrimTag(tag);

            var validateTagResult = tagService.ValidateTagId(DataSetName, tag.Id);
            if (validateTagResult.IsFailure)
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, validateTagResult.Error);
            }

            if (tagService.IsExist(DataSetName, tag.Id))
            {
                return HttpErrorResult(
                        StatusCodes.Status409Conflict,
                        string.Format(TagResources.TagWithId_0_AlreadyExists, tag.Id));
            }

            if (!tagService.ValidateParentTag(DataSetName, tag.ParentId))
            {
                return HttpErrorResult(
                        StatusCodes.Status406NotAcceptable,
                        string.Format(TagResources.TagWithParentId_0_DoesNotFound, tag.ParentId));
            }

            var createdTag = tagService.Create(DataSetName, tag);

            return CreatedAtRoute("GetTag", new { Controller = "Tags", id = tag.Id }, createdTag);
        }

        [HttpPut("{id}")]
        [SwaggerOperation("UpdateTag")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status406NotAcceptable, "", typeof(ErrorsModel))]
        public IActionResult Put(string id, [FromBody]Tag tag)
        {
            tag = tagService.TrimTag(tag);

            var validateTagResult = tagService.ValidateTagId(DataSetName, tag.Id);
            if (validateTagResult.IsFailure)
            {
                return HttpErrorResult(StatusCodes.Status400BadRequest, validateTagResult.Error);
            }

            if (!tagService.IsExist(DataSetName, id))
            {
                return HttpErrorResult(
                        StatusCodes.Status404NotFound,
                        string.Format(TagResources.TagWithId_0_DoesNotFound, id));
            }

            if (id != tag.Id && 
                tagService.IsExist(DataSetName, tag.Id))
            {
                return HttpErrorResult(
                        StatusCodes.Status406NotAcceptable,
                        string.Format(TagResources.TagWithId_0_AlreadyExists, tag.Id));
            }

            if (!string.IsNullOrWhiteSpace(tag.ParentId) &&
                !tagService.IsExist(DataSetName, tag.ParentId))
            {
                return HttpErrorResult(
                        StatusCodes.Status406NotAcceptable, 
                        string.Format(TagResources.TagWithParentId_0_DoesNotFound, tag.ParentId));
            }

            tagService.Update(DataSetName, id, tag);

            return new OkResult();
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        [SwaggerOperation("DeleteTag")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status404NotFound, "", typeof(ErrorsModel))]
        [SwaggerResponse(StatusCodes.Status409Conflict, "", typeof(ErrorsModel))]
        public IActionResult Delete(string id, [FromQuery]bool force = false, [FromQuery]bool cleanDocuments = false)
        {
            if (!tagService.IsExist(DataSetName, id))
            {
                return HttpErrorResult(
                        StatusCodes.Status404NotFound,
                        string.Format(TagResources.TagWithId_0_DoesNotFound, id));
            }

            if (tagService.HasChildren(DataSetName, id) && !force)
            {
                return HttpErrorResult(
                    StatusCodes.Status409Conflict,
                    string.Format(TagResources.Tag_0_IsNotALeafElementProvideForceTrue, id));
            }

            tagService.Delete(DataSetName, id, force, cleanDocuments);

            return new OkResult();
        }
    }
}
