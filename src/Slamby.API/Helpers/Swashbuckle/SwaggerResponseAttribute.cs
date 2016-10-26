using Swashbuckle.SwaggerGen.Annotations;
using Swashbuckle.SwaggerGen.Generator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Swashbuckle.Swagger.Model;

namespace Slamby.API.Helpers.Swashbuckle
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class SwaggerResponseAttribute : Attribute, IOperationFilter
    {
        public SwaggerResponseAttribute(HttpStatusCode statusCode)
            :this((int)statusCode)
        {
        }

        public SwaggerResponseAttribute(HttpStatusCode statusCode, string description = null, Type type = null)
            : this(statusCode)
        {
            Description = description;
            Type = type;
        }

        public SwaggerResponseAttribute(int statusCode)
        {
            StatusCode = statusCode;
        }

        public SwaggerResponseAttribute(int statusCode, string description = null, Type type = null)
            : this(statusCode)
        {
            Description = description;
            Type = type;
        }

        public int StatusCode { get; private set; }

        public string Description { get; set; }

        public Type Type { get; set; }

        public void Apply(Operation operation, OperationFilterContext context)
        {
            var statusCode = StatusCode.ToString();

            operation.Responses[statusCode] = new Response
            {
                Description = Description ?? InferDescriptionFrom(statusCode),
                Schema = (Type != null) ? context.SchemaRegistry.GetOrRegister(Type) : null
            };
        }

        private string InferDescriptionFrom(string statusCode)
        {
            HttpStatusCode enumValue;
            if (Enum.TryParse(statusCode, true, out enumValue))
            {
                return enumValue.ToString();
            }
            return null;
        }
    }
}
