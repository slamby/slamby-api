using Swashbuckle.SwaggerGen.Generator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Swashbuckle.Swagger.Model;

namespace Slamby.API.Helpers.Swashbuckle
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SwaggerResponseRemoveDefaultsAttribute : Attribute, IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            operation.Responses.Clear();
        }
    }
}
