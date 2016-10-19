using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.Controllers;
using Microsoft.AspNet.Mvc.Filters;
using Slamby.API.Resources;
using Slamby.SDK.Net.Models;

namespace Slamby.API.Filters
{
    public class ModelValidationFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            List<string> errors = new List<string>();
            var actionArguments = ((Dictionary<string, object>)context.ActionArguments);

            if (actionArguments.ContainsValue(null))
            {
                foreach (var argument in actionArguments.Where(kv => kv.Value == null))
                {
                    var parameterDescriptor = context.ActionDescriptor.Parameters
                        .Where(w => w.Name == argument.Key)
                        .Cast<ControllerParameterDescriptor>()
                        .FirstOrDefault();
                    if (!parameterDescriptor.ParameterInfo.HasDefaultValue)
                    {
                        errors.Add(string.Format(GlobalResources.TheArgumentCannotBeNull_0, argument.Key));
                    }
                }
            }

            if (!context.ModelState.IsValid)
            {
                errors.AddRange(context.ModelState.SelectMany(m => m.Value.Errors.Select(e =>
                        {
                            return string.IsNullOrEmpty(e.ErrorMessage) 
                                ? e?.Exception.Message ?? string.Empty 
                                : e.ErrorMessage;
                        }
                    )));
            }

            if (errors.Any())
            {
                context.Result = new BadRequestObjectResult(ErrorsModel.Create(errors));
            }

            base.OnActionExecuting(context);
        }
    }
}
