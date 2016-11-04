using System;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Resources;
using Slamby.Common.Helpers;
using Slamby.Elastic.Queries;
using Slamby.SDK.Net.Models;
using Swashbuckle.SwaggerGen.Annotations;

namespace Slamby.API.Controllers
{
    [Route("api/[controller]")]
    [SwaggerGroup("Helper")]
    [SwaggerResponseRemoveDefaults]
    public class HelperController : BaseController
    {
        readonly FileParserQuery fileParserQuery;

        public HelperController(FileParserQuery fileParserQuery)
        {
            this.fileParserQuery = fileParserQuery;
        }

        [HttpPost("FileParser")]
        [SwaggerOperation("FileParser")]
        [SwaggerResponse(StatusCodes.Status200OK, "", typeof(FileParserResult))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "", typeof(ErrorsModel))]
        public IActionResult Post([FromBody]FileParser fileParser)
        {
            if (fileParser?.Content == null)
            {
                return BadRequest(
                    ErrorsModel.Create(
                        string.Format(GlobalResources.TheArgumentCannotBeNull_0, nameof(FileParser.Content))
                        ));
            }

            var content = fileParser.Content.CleanBase64();
            if (!content.IsBase64())
            {
                return BadRequest(
                    ErrorsModel.Create(
                        string.Format(GlobalResources.TheArgument_0_IsNot_1_Type, nameof(FileParser.Content), "base64")
                        ));
            }

            var fieldValues = fileParserQuery.ParseDocument(content);
            var result = new FileParserResult();

            foreach (var field in fieldValues)
            {
                var key = field.Key;
                var values = field.Value;

                switch (key)
                {
                    case "content_type":
                        result.ContentType = string.Concat(values.Cast<string>());
                        break;
                    case "content":
                        result.Content = string.Concat(values.Cast<string>());
                        break;
                    case "content_length":
                        result.ContentLength = (int)values.Cast<double>().Sum();
                        break;
                    case "language":
                        result.Language = string.Concat(values.Cast<string>());
                        break;
                    case "keywords":
                        result.Keywords = string.Concat(values.Cast<string>());
                        break;
                    case "author":
                        result.Author = string.Concat(values.Cast<string>());
                        break;
                    case "date":
                        if (values.Any())
                        {
                            var pattern = CultureInfo.InvariantCulture.DateTimeFormat.UniversalSortableDateTimePattern;
                            result.Date = values.Cast<DateTime>().First().ToString(pattern);
                        }
                        break;
                    case "title":
                        result.Title = string.Concat(values.Cast<string>());
                        break;
                }
            }

            return Ok(result);
        }
    }
}
