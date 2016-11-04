using Swashbuckle.SwaggerGen.Generator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Swashbuckle.Swagger.Model;

namespace Slamby.API.Helpers.Swashbuckle
{
    public class ApplySwaggerResponseFilterAttributesOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            var controllerAttributes = context.ApiDescription.GetControllerAttributes().OfType<SwaggerResponseRemoveDefaultsAttribute>();

            foreach (var attribute in controllerAttributes)
            {
                attribute.Apply(operation, context);
            }

            var operationAttributes = context.ApiDescription.GetActionAttributes().OfType<SwaggerResponseAttribute>();

            foreach (var attribute in operationAttributes)
            {
                attribute.Apply(operation, context);
            }
        }
    }
}
